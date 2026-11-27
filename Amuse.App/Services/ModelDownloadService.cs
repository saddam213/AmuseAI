using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    /// <summary>
    /// Queues and downloads models one at a time, with the shared components they need, and prepares a pipeline's models before it loads.
    /// </summary>
    public sealed class ModelDownloadService : ServiceBase, IModelDownloadService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly Channel<DownloadQueueItem> _downloadQueue;
        private readonly ObservableCollection<DownloadQueueItem> _downloadItems;
        private readonly DownloadService _downloadService;
        private readonly NavigationService _navigationService;
        private readonly IModelAccessService _modelAccessService;
        private bool _isDownloading;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelDownloadService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="modelAccessService">The model access service.</param>
        /// <param name="logger">The logger.</param>
        public ModelDownloadService(Settings settings, DownloadService downloadService, NavigationService navigationService, IModelAccessService modelAccessService, ILogger<ModelDownloadService> logger)
        {
            _logger = logger;
            _settings = settings;
            _downloadService = downloadService;
            _navigationService = navigationService;
            _modelAccessService = modelAccessService;
            _downloadItems = new ObservableCollection<DownloadQueueItem>();
            _downloadItems.CollectionChanged += OnQueueChanged;
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
            private set { SetProperty(ref _isDownloading, value); }
        }


        /// <summary>
        /// Cancels a queued or running download and removes it from the queue, a generate model returns to an installed checkpoint set.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        public async Task CancelAsync(DownloadQueueItem queueItem)
        {
            if (queueItem == null)
                return;

            queueItem.Cancel();
            RemoveQueueItem(queueItem);
            await UpdateStatusAsync(queueItem, ResetCancelledModel(queueItem));
        }


        /// <summary>
        /// Cancels the download of a model, if it is in the queue.
        /// </summary>
        /// <param name="model">The model.</param>
        public Task CancelAsync(IDownloadModel model)
        {
            var queueItem = FindQueueItem(model);
            if (queueItem == null)
                return Task.CompletedTask;

            return CancelAsync(queueItem);
        }


        /// <summary>
        /// Cancels every download in the queue, the last queued first.
        /// </summary>
        public async Task CancelAllAsync()
        {
            foreach (var queueItem in _downloadItems.OrderByDescending(x => x.Index).ToList())
            {
                await CancelAsync(queueItem);
            }
        }


        /// <summary>
        /// Stops processing the queue when the app closes.
        /// </summary>
        public void Shutdown()
        {
            _cancellationTokenSource.SafeCancel();
            _downloadItems.Clear();
        }


        /// <summary>
        /// Queues the model for download, a generate model downloads its current checkpoint set.
        /// The shared components the model needs are queued first, an installed model only queues its missing components.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns><c>true</c> when a download was queued.</returns>
        public async Task<bool> QueueAsync(IDownloadModel model)
        {
            if (IsDownloadPending(model))
                return false;

            var components = GetSharedComponents(model);
            if (components is null)
                return await CreateQueueItemAsync(model);

            var componentsQueued = false;
            foreach (var component in components)
            {
                if (component.Status == ModelStatusType.Installed || IsDownloadPending(component))
                    continue;

                if (!await CreateQueueItemAsync(component))
                    return false;

                componentsQueued = true;
            }

            if (IsModelInstalled(model))
                return componentsQueued;

            return await CreateQueueItemAsync(model);
        }


        /// <summary>
        /// Prepares a pipeline to load, asking to queue the downloads it is missing and which checkpoint set of the generate model to download.
        /// </summary>
        /// <param name="model">The generate model, or <c>null</c> for an upscale or extract only pipeline.</param>
        /// <param name="checkpointType">The checkpoint set type of the generate model to load.</param>
        /// <param name="pipelineModels">The other models the pipeline needs, e.g. LoRA adapters, a ControlNet or an upscaler.</param>
        /// <returns><c>true</c> when everything is installed and the pipeline can be loaded.</returns>
        public async Task<bool> PrepareLoadAsync(IGenerateModel model, DataType? checkpointType, IReadOnlyList<IDownloadModel> pipelineModels)
        {
            if (IsDownloadQueued(model) || pipelineModels.Any(IsDownloadQueued))
            {
                await DialogService.ShowMessageAsync(AppDefault.ModelDownloadingTitle, AppDefault.PipelineDownloadingMessage, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
                return false;
            }

            var isModelInstalled = model is null || model.IsCheckpointInstalled(checkpointType, _settings);
            if (model is not null && isModelInstalled)
                await SetCurrentCheckpointAsync(model, checkpointType);

            var missingModels = pipelineModels.Where(x => x.Status != ModelStatusType.Installed).ToList();
            if (isModelInstalled && missingModels.Count == 0)
                return true;

            var dialog = DialogService.GetDialog<CheckpointDialog>();
            if (!await dialog.ShowDialogAsync(model, checkpointType, !isModelInstalled && model.HasCheckpointSets))
                return false;

            if (!isModelInstalled && !model.IsCheckpointInstalled(dialog.SelectedCheckpoint?.Type, _settings) && !await _modelAccessService.RequestModelReleaseAsync(model))
                return false;

            if (!isModelInstalled)
            {
                await SetCurrentCheckpointAsync(model, dialog.SelectedCheckpoint?.Type);
                isModelInstalled = model.Status == ModelStatusType.Installed;
            }

            if (isModelInstalled && missingModels.Count == 0)
                return true;

            foreach (var missingModel in missingModels)
            {
                if (!await QueueAsync(missingModel))
                    return false;
            }

            if (!isModelInstalled && !await QueueAsync(model))
                return false;

            await _navigationService.NavigateAsync((int)View.Models);
            return false;
        }


        /// <summary>
        /// Queues the download of a model, asking which checkpoint set to download when a generate model has more than one, a failed download is retried as is.
        /// </summary>
        /// <param name="model">The model.</param>
        public async Task<bool> DownloadAsync(IDownloadModel model)
        {
            if (model is not IGenerateModel generateModel || !generateModel.HasCheckpointSets || model.Status == ModelStatusType.DownloadFailed)
                return await QueueAsync(model);

            var checkpointType = generateModel.GetCheckpoints().FirstOrDefault(x => !generateModel.IsCheckpointInstalled(x.Type, _settings))?.Type ?? generateModel.CurrentCheckpointType;
            var dialog = DialogService.GetDialog<CheckpointDialog>();
            if (!await dialog.ShowDialogAsync(generateModel, checkpointType, true))
                return false;

            if (!generateModel.IsCheckpointInstalled(dialog.SelectedCheckpoint?.Type, _settings) && !await _modelAccessService.RequestModelReleaseAsync(generateModel))
                return false;

            await SetCurrentCheckpointAsync(generateModel, dialog.SelectedCheckpoint?.Type);
            return await QueueAsync(generateModel);
        }


        /// <summary>
        /// Asks whether to download the files a newly added model is missing, its shared components and its own files, and queues them on yes.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns><c>true</c> when a download was queued.</returns>
        public async Task<bool> PromptDownloadAsync(IDownloadModel model)
        {
            var missingModels = GetMissingModels(model);
            if (missingModels.Count == 0)
                return false;

            var modelNames = string.Join(Environment.NewLine, missingModels.Select(x => $"• {x.Name}"));
            if (!await DialogService.ShowMessageAsync(AppDefault.DownloadNowTitle, string.Format(AppDefault.DownloadNowMessage, model.Name, modelNames), MessageDialogType.YesNo, MessageBoxIconType.Question, MessageBoxStyleType.Info))
                return false;

            return await QueueAsync(model);
        }


        /// <summary>
        /// Gets the models a model needs downloaded before it can be used, its shared components that are not installed and the model when its own online files are not installed.
        /// </summary>
        /// <param name="model">The model.</param>
        private List<IDownloadModel> GetMissingModels(IDownloadModel model)
        {
            var missingModels = new List<IDownloadModel>();
            var components = GetSharedComponents(model);
            if (components is not null)
                missingModels.AddRange(components.Distinct().Where(x => x.Status != ModelStatusType.Installed && !IsDownloadQueued(x)));

            if (HasOnlineFiles(model) && !IsModelInstalled(model) && !IsDownloadQueued(model))
                missingModels.Add(model);

            return missingModels;
        }


        /// <summary>
        /// Determines whether a model downloads files of its own, a generate model or upscaler made only of local files and shared components does not.
        /// </summary>
        /// <param name="model">The model.</param>
        private static bool HasOnlineFiles(IDownloadModel model)
        {
            var checkpoints = model switch
            {
                IGenerateModel generateModel => generateModel.GetComponents(),
                UpscaleModel upscaleModel => upscaleModel.GetComponents(),
                _ => null
            };
            return checkpoints is null || checkpoints.Any(x => x.Type == CheckpointType.OnlineFile || x.Type == CheckpointType.OnlineFolder);
        }


        /// <summary>
        /// Gets the shared components the model's checkpoints reference, resolved before anything is queued.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The components, or <c>null</c> when the model type has no shared components.</returns>
        /// <exception cref="Exception">A referenced component is not in the settings.</exception>
        private List<ComponentModel> GetSharedComponents(IDownloadModel model)
        {
            var checkpointComponents = model switch
            {
                IGenerateModel generateModel => generateModel.GetComponents(),
                UpscaleModel upscaleModel => upscaleModel.GetComponents(),
                _ => null
            };
            if (checkpointComponents is null)
                return null;

            var components = new List<ComponentModel>();
            foreach (var checkpointComponent in checkpointComponents.Where(x => x.Type == CheckpointType.Component))
            {
                var component = _settings.Components.FirstOrDefault(x => x.Key == checkpointComponent.Path);
                if (component == null)
                    throw new Exception(string.Format(AppErrors.CheckpointResolveComponent, checkpointComponent.Path));

                components.Add(component);
            }
            return components;
        }


        /// <summary>
        /// Determines whether the model has a download queued or running. A failed download is removed from the queue, so the model can be queued again.
        /// </summary>
        /// <param name="model">The model.</param>
        private bool IsDownloadPending(IDownloadModel model)
        {
            var queueItem = FindQueueItem(model);
            if (queueItem == null)
                return false;

            if (!queueItem.IsFailed)
                return true;

            queueItem.Cancel();
            RemoveQueueItem(queueItem);
            return false;
        }


        /// <summary>
        /// Finds the queue item of a model, models of different types can share an id.
        /// </summary>
        /// <param name="model">The model.</param>
        private DownloadQueueItem FindQueueItem(IDownloadModel model)
        {
            return _downloadItems.FirstOrDefault(x => x.DownloadModel.GetType() == model.GetType() && x.DownloadModel.Id == model.Id);
        }


        /// <summary>
        /// Determines whether the model is queued for download or downloading, a failed download is not, it is queued again when the pipeline loads.
        /// </summary>
        /// <param name="model">The model, or <c>null</c>.</param>
        private static bool IsDownloadQueued(IDownloadModel model)
        {
            return model?.Status == ModelStatusType.DownloadQueue || model?.Status == ModelStatusType.Downloading;
        }


        /// <summary>
        /// Sets the current checkpoint set of the model and saves the settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="checkpointType">The checkpoint set type.</param>
        private async Task SetCurrentCheckpointAsync(IGenerateModel model, DataType? checkpointType)
        {
            model.SetCurrentCheckpoint(checkpointType, _settings);
            await _settings.SaveAsync();
        }


        /// <summary>
        /// Determines whether the model is installed, a generate model checks the files of its current checkpoint set.
        /// </summary>
        /// <param name="model">The model.</param>
        private bool IsModelInstalled(IDownloadModel model)
        {
            if (model is IGenerateModel generateModel)
                return generateModel.IsCheckpointInstalled(null, _settings);

            return model.Status == ModelStatusType.Installed;
        }


        /// <summary>
        /// Switches a generate model back to an installed checkpoint set after a cancelled download, and gets the status it returns to.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        private ModelStatusType ResetCancelledModel(DownloadQueueItem queueItem)
        {
            if (queueItem.DownloadModel is not IGenerateModel generateModel)
                return ModelStatusType.Available;

            generateModel.ResetCurrentCheckpoint(_settings);
            return IsModelInstalled(generateModel)
                ? ModelStatusType.Installed
                : ModelStatusType.Available;
        }


        /// <summary>
        /// Adds the model to the end of the download queue.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns><c>true</c> when the model was queued.</returns>
        private async Task<bool> CreateQueueItemAsync(IDownloadModel model)
        {
            var queueItem = new DownloadQueueItem(GetNextIndex(), model);
            await UpdateStatusAsync(queueItem, ModelStatusType.DownloadQueue);
            if (_downloadQueue.Writer.TryWrite(queueItem))
            {
                _downloadItems.Add(queueItem);
                return true;
            }
            await UpdateStatusAsync(queueItem, ModelStatusType.DownloadFailed);
            return false;
        }


        /// <summary>
        /// Downloads the files of a queued model. A cancelled download was already removed and reset by the cancel, the status is only
        /// reset here when the cancel landed before the download was marked as downloading,
        /// a failed download stays in the queue to be retried. The models are scanned after a download, so a model whose shared component
        /// is missing is not left as installed, and a model that was waiting on the downloaded component becomes installed.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        private async Task ExecuteDownloadAsync(DownloadQueueItem queueItem)
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                if (queueItem.CancellationToken.IsCancellationRequested)
                    return;

                IsDownloading = true;
                _logger.LogInformation("[ModelDownload] [Download] Downloading {Name}...", queueItem.Name);
                await UpdateStatusAsync(queueItem, ModelStatusType.Downloading);
                queueItem.Progress.Indeterminate();

                if (queueItem.DownloadModel is ComponentModel componentModel)
                {
                    await DownloadComponentAsync(queueItem, _settings.DirectoryModel, componentModel);
                }
                else if (queueItem.DownloadModel is UpscaleModel upscaleModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryUpscale, upscaleModel.Checkpoint);
                    if (upscaleModel.Vae is not null)
                        await DownloadCheckpointAsync(queueItem, _settings.DirectoryUpscale, upscaleModel.Vae);
                }
                else if (queueItem.DownloadModel is ExtractModel extractModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryExtract, extractModel.Checkpoint);
                }
                else if (queueItem.DownloadModel is ControlNetModel controlNetModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryControlNet, controlNetModel.Checkpoint);
                }
                else if (queueItem.DownloadModel is LoraAdapterModel loraAdapterModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryLoraAdapter, loraAdapterModel.Checkpoint);
                }
                else if (queueItem.DownloadModel is DiffusionModel diffusionModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryDiffusion, diffusionModel);
                }
                else if (queueItem.DownloadModel is LanguageModel languageModel)
                {
                    await DownloadCheckpointAsync(queueItem, _settings.DirectoryLangaugeModel, languageModel);
                }

                queueItem.CancellationToken.ThrowIfCancellationRequested();
                RemoveQueueItem(queueItem);
                queueItem.UpdateStatus(ModelStatusType.Installed);
                App.Current.Dispatcher.Invoke(_settings.ScanModels);
                await _settings.SaveAsync();
                _logger.LogInformation("[ModelDownload] [Download] Downloading {Name} complete, Elapsed: {Elapsed:c}", queueItem.Name, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException) when (queueItem.CancellationToken.IsCancellationRequested)
            {
                if (queueItem.Status == ModelStatusType.Downloading)
                    await UpdateStatusAsync(queueItem, ResetCancelledModel(queueItem));

                _logger.LogInformation("[ModelDownload] [Download] Downloading {Name} cancelled, Elapsed: {Elapsed:c}", queueItem.Name, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelDownload] [Download] An exception occurred downloading {Name}, Elapsed: {Elapsed:c}", queueItem.Name, Stopwatch.GetElapsedTime(timestamp));
                queueItem.Progress.Clear();
                queueItem.ErrorMessage = ex.Message;
                queueItem.IsFailed = true;
                await UpdateStatusAsync(queueItem, ModelStatusType.DownloadFailed);
            }
            finally
            {
                IsDownloading = false;
            }
        }


        /// <summary>
        /// Downloads the files of a single checkpoint, unless they are installed.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        /// <param name="directory">The model directory.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        private async Task DownloadCheckpointAsync(DownloadQueueItem queueItem, string directory, CheckpointComponent checkpoint)
        {
            if (checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.OnlineFile)
            {
                if (!checkpoint.IsInstalled(_settings, directory))
                {
                    var output = CheckpointComponent.GetSafePath(directory, checkpoint.Folder, checkpoint.Path);
                    await _downloadService.DownloadAsync([.. checkpoint.DownloadFiles], output, queueItem.ProgressCallback, queueItem.CancellationToken);
                }
            }
        }


        /// <summary>
        /// Downloads the files of a shared component, unless they are installed.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        /// <param name="directory">The model directory.</param>
        /// <param name="component">The component.</param>
        private async Task DownloadComponentAsync(DownloadQueueItem queueItem, string directory, ComponentModel component)
        {
            var checkpoint = component.Checkpoint;
            if (checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.OnlineFile)
            {
                if (!checkpoint.IsInstalled(_settings, directory))
                {
                    var output = CheckpointComponent.GetSafePath(directory, checkpoint.Folder, checkpoint.Path);
                    await _downloadService.DownloadAsync([.. checkpoint.DownloadFiles], output, queueItem.ProgressCallback, queueItem.CancellationToken);
                }
            }
        }


        /// <summary>
        /// Downloads the missing files of a generate model's current checkpoint set, shared components are downloaded separately.
        /// The download service maps each output folder's files by their common url prefix, so checkpoints that share an output folder
        /// go in separate batches, otherwise files from different url folders (the Wan 2.2 LowNoise and HighNoise experts) land in
        /// sub folders instead of where <see cref="CheckpointComponent.IsInstalled"/> and <see cref="CheckpointComponent.Resolve"/> look.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        /// <param name="directory">The model directory.</param>
        /// <param name="checkpointModel">The generate model.</param>
        private async Task DownloadCheckpointAsync(DownloadQueueItem queueItem, string directory, IGenerateModel checkpointModel)
        {
            var checkpointBatches = new List<Dictionary<string, string[]>>();
            foreach (var checkpoint in checkpointModel.GetComponents())
            {
                if (checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.OnlineFile)
                {
                    if (!checkpoint.IsInstalled(_settings, directory))
                    {
                        var output = CheckpointComponent.GetSafePath(directory, checkpoint.Folder, checkpoint.Path);
                        var checkpointFiles = checkpointBatches.FirstOrDefault(x => !x.ContainsKey(output));
                        if (checkpointFiles == null)
                        {
                            checkpointFiles = new Dictionary<string, string[]>();
                            checkpointBatches.Add(checkpointFiles);
                        }
                        checkpointFiles.Add(output, checkpoint.DownloadFiles);
                    }
                }
            }

            foreach (var checkpointFiles in checkpointBatches)
            {
                await _downloadService.DownloadAsync(checkpointFiles, queueItem.ProgressCallback, queueItem.CancellationToken);
            }
        }


        /// <summary>
        /// Sets the status of the queue item's model and saves the settings.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        /// <param name="status">The status.</param>
        private async Task UpdateStatusAsync(DownloadQueueItem queueItem, ModelStatusType status)
        {
            queueItem.UpdateStatus(status);
            await _settings.SaveAsync();
        }


        /// <summary>
        /// Downloads the queued models one at a time until the app closes, a download that throws does not stop the queue.
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var queueItem in _downloadQueue.Reader.ReadAllAsync(_cancellationTokenSource.Token))
                {
                    try
                    {
                        await ExecuteDownloadAsync(queueItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ModelDownload] [Queue] An exception occurred processing {Name}, the queue continues", queueItem.Name);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }


        /// <summary>
        /// Removes the queue item on the UI thread, the queue is bound to the view.
        /// </summary>
        /// <param name="queueItem">The queue item.</param>
        private void RemoveQueueItem(DownloadQueueItem queueItem)
        {
            App.Current.Dispatcher.Invoke(() => _downloadItems.Remove(queueItem));
        }


        /// <summary>
        /// Notifies the properties computed from the queue when it changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
        private void OnQueueChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyPropertyChanged(nameof(CanCancel));
            NotifyPropertyChanged(nameof(QueueLength));
        }


        /// <summary>
        /// Gets the index of the next queue item, one after the last queued.
        /// </summary>
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

        Task<bool> QueueAsync(IDownloadModel model);
        Task<bool> DownloadAsync(IDownloadModel model);
        Task<bool> PromptDownloadAsync(IDownloadModel model);
        Task<bool> PrepareLoadAsync(IGenerateModel model, DataType? checkpointType, IReadOnlyList<IDownloadModel> pipelineModels);
        Task CancelAsync(IDownloadModel model);
    }
}
