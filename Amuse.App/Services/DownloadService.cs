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

namespace Amuse.App.Services
{
    public class DownloadService : ServiceBase, IDownloadService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly IEnvironmentService _environmentService;
        private readonly Channel<DownloadQueueItem> _downloadQueue;
        private readonly ObservableCollection<DownloadQueueItem> _downloadItems;

        private bool _isDownloading;
        private CancellationTokenSource _cancellationTokenSource;

        public DownloadService(Settings settings, IEnvironmentService environmentService, ILogger<DownloadService> logger)
        {
            _logger = logger;
            _settings = settings;
            _environmentService = environmentService;
            _downloadItems = new ObservableCollection<DownloadQueueItem>();
            _downloadQueue = Channel.CreateUnbounded<DownloadQueueItem>();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(ProcessQueueAsync);
        }

        public bool IsDownloading
        {
            get { return _isDownloading; }
            private set { SetProperty(ref _isDownloading, value); NotifyPropertyChanged(nameof(CanCancel)); NotifyPropertyChanged(nameof(QueueLength)); }
        }

        public int QueueLength => _downloadItems.Count;
        public bool CanCancel => _downloadItems.Count > 0;
        public ObservableCollection<DownloadQueueItem> Queue => _downloadItems;

        public void Shutdown()
        {
            _cancellationTokenSource.SafeCancel();
            _downloadItems.Clear();
        }


        public async Task<bool> QueueAsync(DiffusionModel model, bool isVerify)
        {
            if (_downloadItems.Any(x => x.DiffusionModel.Id == model.Id))
                return false;

            var index = GetNextIndex();
            var queueItem = new DownloadQueueItem(index, model, isVerify);
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


        public Task CancelAsync(DiffusionModel model)
        {
            var queueItem = _downloadItems.FirstOrDefault(x => x.DiffusionModel.Id == model.Id);
            if (queueItem == null)
                return Task.CompletedTask;

            return CancelAsync(queueItem);
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


        public bool CanQueueItem(DiffusionModel model)
        {
            return _environmentService.IsInstalled();
        }


        private async Task ExecuteDownloadAsync(DownloadQueueItem queueItem)
        {
            if (queueItem.CancellationToken.IsCancellationRequested)
                return;

            IsDownloading = true;
            var model = queueItem.DiffusionModel;
            await UpdateStatus(queueItem, queueItem.IsVerify ? ModelStatusType.Verifying : ModelStatusType.Downloading);

            try
            {
                queueItem.Progress.Indeterminate();
                var downloadClient = await _environmentService.CreateDownloadClientAsync(queueItem.ProgressCallback, queueItem.CancellationToken);
                await downloadClient.DownloadAsync(new PipelineConfig
                {
                    Variant = model.Variant,
                    BaseModelPath = model.Path,
                    Pipeline = model.Pipeline,
                    DataType = model.BaseType,
                    CacheDirectory = Path.GetFullPath(_settings.DirectoryCache),
                    SecureToken = _settings.SecureToken,
                    MemoryMode = MemoryModeType.OffloadCPU,
                    CheckpointConfig = model.Checkpoint.ToConfig(),
                    ProcessType = model.ProcessTypes.FirstOrDefault()
                }, queueItem.CancellationToken);
                await downloadClient.StopAsync();

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
            queueItem.DiffusionModel.Status = status;
            queueItem.NotifyPropertyChanged(nameof(DiffusionModel.Status));
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
    }


    public interface IDownloadService
    {
        bool IsDownloading { get; }
        bool CanCancel { get; }
        ObservableCollection<DownloadQueueItem> Queue { get; }

        void Shutdown();
        Task<bool> QueueAsync(DiffusionModel model, bool isVerify);
        Task CancelAllAsync();
        Task CancelAsync(DiffusionModel model);
        Task CancelAsync(DownloadQueueItem model);
        bool CanQueueItem(DiffusionModel model);
    }
}
