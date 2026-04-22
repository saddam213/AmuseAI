using Amuse.App.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Python.Common;
using TensorStack.Python.Config;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    public class ModelDownloadService : ServiceBase, IModelDownloadService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly IEnvironmentService _environmentService;
        private readonly Channel<DownloadQueueItem> _downloadQueue;
        private readonly ObservableCollection<DownloadQueueItem> _downloadItems;
        private readonly DownloadService _downloadService;
        private bool _isDownloading;
        private CancellationTokenSource _cancellationTokenSource;

        public ModelDownloadService(Settings settings, IEnvironmentService environmentService, DownloadService downloadService, ILogger<ModelDownloadService> logger)
        {
            _logger = logger;
            _settings = settings;
            _downloadService = downloadService;
            _environmentService = environmentService;
            _downloadItems = new ObservableCollection<DownloadQueueItem>();
            _downloadQueue = Channel.CreateUnbounded<DownloadQueueItem>();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(ProcessQueueAsync);
        }

        public int QueueLength => _downloadItems.Count;
        public bool CanCancel => _downloadItems.Count > 0;
        public ObservableCollection<DownloadQueueItem> Queue => _downloadItems;
        public bool IsDownloading
        {
            get { return _isDownloading; }
            private set { SetProperty(ref _isDownloading, value); NotifyPropertyChanged(nameof(CanCancel)); NotifyPropertyChanged(nameof(QueueLength)); }
        }

        public void Shutdown()
        {
            _cancellationTokenSource.SafeCancel();
            _downloadItems.Clear();
        }


        public async Task CancelAsync(DownloadQueueItem queueItem)
        {
            queueItem.Cancel();
            RemoveQueueItem(queueItem);
            await UpdateStatus(queueItem, ModelStatusType.Pending);
        }


        public async Task CancelAllAsync()
        {
            foreach (var queueItem in _downloadItems.OrderByDescending(x => x.Index))
            {
                await CancelAsync(queueItem);
            }
        }


        public async Task<bool> QueueAsync<T>(T model, bool isVerify) where T : IDownloadModel
        {
            if (_downloadItems.Any(x => x.DownloadModel is T && x.DownloadModel.Id == model.Id))
                return false;

            var index = GetNextIndex();
            var queueItem = new DownloadQueueItem(index, model, isVerify);
            return await QueueItem(queueItem);
        }


        public Task CancelAsync<T>(T model) where T : IDownloadModel
        {
            var queueItem = _downloadItems.FirstOrDefault(x => x.DownloadModel is DiffusionModel && x.DownloadModel.Id == model.Id);
            if (queueItem == null)
                return Task.CompletedTask;

            return CancelAsync(queueItem);
        }


        public bool CanQueueItem<T>(T model) where T : IDownloadModel
        {
            return _environmentService.IsInstalled();
        }


        private async Task<bool> QueueItem(DownloadQueueItem queueItem)
        {
            await UpdateStatus(queueItem, ModelStatusType.DownloadQueue);
            if (_downloadQueue.Writer.TryWrite(queueItem))
            {
                _downloadItems.Add(queueItem);
                NotifyPropertyChanged(nameof(CanCancel));
                NotifyPropertyChanged(nameof(QueueLength));
                return true;
            }
            await UpdateStatus(queueItem, ModelStatusType.DownloadFailed);
            return false;
        }


        private async Task ExecuteDownloadAsync(DownloadQueueItem queueItem)
        {
            try
            {
                if (queueItem.CancellationToken.IsCancellationRequested)
                    return;

                IsDownloading = true;
                await UpdateStatus(queueItem, queueItem.IsVerify ? ModelStatusType.Verifying : ModelStatusType.Downloading);
                queueItem.Progress.Indeterminate();

                if (queueItem.DownloadModel is ExtractModel extractModel)
                {
                    await _downloadService.DownloadAsync([.. extractModel.UrlPaths], Path.Combine(_settings.DirectoryModel, "Extract", extractModel.Name), CreateProgressCallback(queueItem), queueItem.CancellationToken);
                }
                else if (queueItem.DownloadModel is UpscaleModel upscaleModel)
                {
                    await _downloadService.DownloadAsync([.. upscaleModel.UrlPaths], Path.Combine(_settings.DirectoryModel, "Upscale", upscaleModel.Name), CreateProgressCallback(queueItem), queueItem.CancellationToken);
                }
                else if (queueItem.DownloadModel is AudioModel audioModel)
                {
                    await _downloadService.DownloadAsync([.. audioModel.UrlPaths], Path.Combine(_settings.DirectoryModel, "Audio", audioModel.Name), CreateProgressCallback(queueItem), queueItem.CancellationToken);
                }
                else
                {
                    var downloadClient = await _environmentService.CreateDownloadClientAsync(queueItem.ProgressCallback, queueItem.CancellationToken);
                    if (queueItem.DownloadModel is DiffusionModel diffusionModel)
                    {
                        await downloadClient.DownloadAsync(new PipelineConfig
                        {
                            Variant = diffusionModel.Variant,
                            BaseModelPath = diffusionModel.Path,
                            Pipeline = diffusionModel.Pipeline,
                            DataType = diffusionModel.BaseType,
                            CacheDirectory = Path.GetFullPath(_settings.DirectoryModel),
                            SecureToken = _settings.SecureToken,
                            MemoryMode = MemoryModeType.OffloadCPU,
                            CheckpointConfig = diffusionModel.Checkpoint.ToConfig(),
                            ProcessType = diffusionModel.ProcessTypes.FirstOrDefault()
                        }, queueItem.CancellationToken);
                    }
                    else if (queueItem.DownloadModel is LoraAdapterModel loraModel)
                    {
                        var models = new LoraAdapterModel[] { loraModel };
                        await downloadClient.DownloadAsync(new PipelineConfig
                        {
                            BaseModelPath = loraModel.Path,
                            Pipeline = loraModel.Pipeline,
                            DataType = DataType.Bfloat16,
                            CacheDirectory = Path.GetFullPath(_settings.DirectoryModel),
                            SecureToken = _settings.SecureToken,
                            MemoryMode = MemoryModeType.OffloadCPU,
                            ProcessType = ProcessType.TextToImage,
                            LoraAdapters = models.GetLoraAdapters()
                        }, queueItem.CancellationToken);
                    }
                    else if (queueItem.DownloadModel is ControlNetModel controlNetModel)
                    {
                        await downloadClient.DownloadAsync(new PipelineConfig
                        {
                            BaseModelPath = controlNetModel.Path,
                            Pipeline = controlNetModel.Pipeline,
                            DataType = DataType.Bfloat16,
                            CacheDirectory = Path.GetFullPath(_settings.DirectoryModel),
                            SecureToken = _settings.SecureToken,
                            MemoryMode = MemoryModeType.OffloadCPU,
                            ProcessType = ProcessType.TextToImage,
                            ControlNet = controlNetModel.GetControlNet()
                        }, queueItem.CancellationToken);
                    }
                    await downloadClient.StopAsync();
                }

                RemoveQueueItem(queueItem);
                await UpdateStatus(queueItem, ModelStatusType.Installed);
            }
            catch (OperationCanceledException)
            {
                await UpdateStatus(queueItem, queueItem.IsVerify ? ModelStatusType.Unknown : ModelStatusType.Pending);
            }
            catch (Exception)
            {
                queueItem.Progress.Clear();
                await UpdateStatus(queueItem, queueItem.IsVerify ? ModelStatusType.Unknown : ModelStatusType.DownloadFailed);
            }
            finally
            {
                IsDownloading = false;
                NotifyPropertyChanged(nameof(CanCancel));
                NotifyPropertyChanged(nameof(QueueLength));
            }
        }


        private async Task UpdateStatus(DownloadQueueItem queueItem, ModelStatusType status)
        {
            queueItem.UpdateStatus(status);
            await SettingsManager.SaveAsync(_settings);
        }


        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var queueItem in _downloadQueue.Reader.ReadAllAsync(_cancellationTokenSource.Token))
                {
                    await ExecuteDownloadAsync(queueItem);
                }
            }
            catch (OperationCanceledException) { }
        }


        private void RemoveQueueItem(DownloadQueueItem queueItem)
        {
            App.Current.Dispatcher.Invoke(() => _downloadItems.Remove(queueItem));
        }


        private int GetNextIndex()
        {
            if (_downloadItems.IsNullOrEmpty())
                return 0;

            return _downloadItems.Max(x => x.Index) + 1;
        }


        private static Progress<DownloadProgress> CreateProgressCallback(DownloadQueueItem queueItem)
        {
            return new Progress<DownloadProgress>((p) => queueItem.ProgressCallback?.Report(new PipelineProgress
            {
                Key = "Download",
                Value = (int)p.FileProgress,
                Maximum = 100,
                BatchValue = (int)p.TotalProgress,
                BatchMaximum = 100,
                Elapsed = p.BytesSec > 0 ? (float)(p.BytesSec / 1_048_576.0) : 0f
            }));
        }
    }


    public interface IModelDownloadService
    {
        bool IsDownloading { get; }
        bool CanCancel { get; }
        ObservableCollection<DownloadQueueItem> Queue { get; }

        void Shutdown();
        Task CancelAllAsync();
        Task CancelAsync(DownloadQueueItem model);

        Task<bool> QueueAsync<T>(T model, bool isVerify) where T : IDownloadModel;
        Task CancelAsync<T>(T model) where T : IDownloadModel;
        bool CanQueueItem<T>(T model) where T : IDownloadModel;
    }
}
