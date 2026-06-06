using Amuse.App.Common;
using Amuse.App.Dialogs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    public sealed class ModelDownloadService : ServiceBase, IModelDownloadService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly Channel<DownloadQueueItem> _downloadQueue;
        private readonly ObservableCollection<DownloadQueueItem> _downloadItems;
        private readonly DownloadService _downloadService;
        private bool _isDownloading;
        private CancellationTokenSource _cancellationTokenSource;

        public ModelDownloadService(Settings settings, DownloadService downloadService, ILogger<ModelDownloadService> logger)
        {
            _logger = logger;
            _settings = settings;
            _downloadService = downloadService;
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


        public async Task CancelAsync(DownloadQueueItem queueItem)
        {
            queueItem.Cancel();
            RemoveQueueItem(queueItem);
            await UpdateStatus(queueItem, ModelStatusType.Available);
        }


        public Task CancelAsync<T>(T model) where T : IDownloadModel
        {
            var queueItem = _downloadItems.FirstOrDefault(x => x.DownloadModel is DiffusionModel && x.DownloadModel.Id == model.Id);
            if (queueItem == null)
                return Task.CompletedTask;

            return CancelAsync(queueItem);
        }


        public async Task CancelAllAsync()
        {
            foreach (var queueItem in _downloadItems.OrderByDescending(x => x.Index))
            {
                await CancelAsync(queueItem);
            }
        }


        public void Shutdown()
        {
            _cancellationTokenSource.SafeCancel();
            _downloadItems.Clear();
        }


        public async Task<bool> IsAccessTokenSetAsync(IDownloadModel model)
        {
            if (string.IsNullOrWhiteSpace(model.AccessToken))
                return true;

            var accessToken = _settings.AccessTokens?.FirstOrDefault(x => x.Name.Equals(model.AccessToken, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(accessToken?.Token))
                return true;

            var dialog = DialogService.GetDialog<GatedModelDialog>();
            await dialog.ShowDialogAsync(model);
            return false;
        }


        public async Task<bool> QueueAsync<T>(T model) where T : IDownloadModel
        {
            if (!await IsAccessTokenSetAsync(model))
                return false;

            var existingDownload = _downloadItems.FirstOrDefault(x => x.DownloadModel is T && x.DownloadModel.Id == model.Id);
            if (existingDownload != null)
            {
                if (existingDownload.Status != ModelStatusType.DownloadFailed)
                    return false;

                await CancelAsync(existingDownload);
            }


            if (model is DiffusionModel diffusionModel)
            {
                var componentsQueued = false;
                foreach (var checkpointComponent in diffusionModel.Checkpoint.GetComponents())
                {
                    if (checkpointComponent.Type != CheckpointType.Component)
                        continue;

                    var component = _settings.Components.FirstOrDefault(x => x.Key == checkpointComponent.Path);
                    if (component == null)
                        continue; // TODO: throw?

                    if (component.Status == ModelStatusType.Installed)
                        continue;

                    if (_downloadItems.Any(x => x.DownloadModel is ComponentModel && x.DownloadModel.Id == component.Id))
                        continue;

                    if (!await CreateQueueItem(component))
                        continue; // TODO: throw?

                    componentsQueued = true;
                }

                // If diffusion model is already installed exit here
                if (diffusionModel.Status == ModelStatusType.Installed)
                    return componentsQueued;
            }

            return await CreateQueueItem(model);
        }


        private async Task<bool> CreateQueueItem<T>(T model) where T : IDownloadModel
        {
            var index = GetNextIndex();
            var queueItem = new DownloadQueueItem(index, model);
            return await AddQueueItem(queueItem);
        }


        private async Task<bool> AddQueueItem(DownloadQueueItem queueItem)
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
                await UpdateStatus(queueItem, ModelStatusType.Downloading);
                queueItem.Progress.Indeterminate();

                var components = _settings.Components;
                if (queueItem.DownloadModel is ComponentModel componentModel)
                {
                    await DownloadComponentAsync(queueItem, componentModel);
                }
                else if (queueItem.DownloadModel is UpscaleModel upscaleModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryUpscale, upscaleModel.Checkpoint, components);
                }
                else if (queueItem.DownloadModel is ExtractModel extractModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryExtract, extractModel.Checkpoint, components);
                }
                else if (queueItem.DownloadModel is ControlNetModel controlNetModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryControlNet, controlNetModel.Checkpoint, components);
                }
                else if (queueItem.DownloadModel is LoraAdapterModel loraAdapterModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryLoraAdapter, loraAdapterModel.Checkpoint, components);
                }
                else if (queueItem.DownloadModel is DiffusionModel diffusionModel)
                {
                    foreach (var checkpointComponent in diffusionModel.Checkpoint.GetComponents())
                    {
                        await DownloadCheckpointAsync(queueItem, _settings.DirectoryDiffusion, checkpointComponent, components);
                    }
                }

                RemoveQueueItem(queueItem);
                await UpdateStatus(queueItem, ModelStatusType.Installed);
            }
            catch (OperationCanceledException)
            {
                await UpdateStatus(queueItem, ModelStatusType.Available);
            }
            catch (Exception ex)
            {
                queueItem.Progress.Clear();
                queueItem.ErrorMessage = ex.Message;
                await UpdateStatus(queueItem, ModelStatusType.DownloadFailed);
            }
            finally
            {
                IsDownloading = false;
                NotifyPropertyChanged(nameof(CanCancel));
                NotifyPropertyChanged(nameof(QueueLength));
            }
        }


        private async Task DownloadCheckpointAsync(DownloadQueueItem queueItem, string directory, CheckpointComponent checkpoint, IReadOnlyCollection<ComponentModel> components)
        {
            if (checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.OnlineFile)
            {
                if (!checkpoint.IsInstalled(directory, components))
                {
                    var output = CheckpointComponent.GetSafePath(directory, checkpoint.Folder, checkpoint.Path);
                    await _downloadService.DownloadAsync([.. checkpoint.DownloadFiles], output, queueItem.ProgressCallback, queueItem.CancellationToken);
                }
            }
        }


        private async Task DownloadComponentAsync(DownloadQueueItem queueItem, ComponentModel component)
        {
            var output = Path.Combine(_settings.DirectoryModel, component.Type, component.Folder);
            await _downloadService.DownloadAsync([.. component.DownloadFiles], output, queueItem.ProgressCallback, queueItem.CancellationToken);
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
    }


    public interface IModelDownloadService
    {
        bool IsDownloading { get; }
        bool CanCancel { get; }
        ObservableCollection<DownloadQueueItem> Queue { get; }

        void Shutdown();
        Task CancelAllAsync();
        Task CancelAsync(DownloadQueueItem model);

        Task<bool> QueueAsync<T>(T model) where T : IDownloadModel;
        Task CancelAsync<T>(T model) where T : IDownloadModel;

        Task<bool> IsAccessTokenSetAsync(IDownloadModel model);
    }
}
