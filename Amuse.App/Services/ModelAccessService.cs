using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    /// <summary>
    /// The one source of truth for which pipeline owns each loaded model. A pipeline asks for access before its models load, and is asked what
    /// to do with every model another pipeline has loaded, whether it stays loaded next to it or is replaced by it.
    /// </summary>
    public sealed class ModelAccessService : IModelAccessService
    {
        private readonly ILogger _logger;
        private readonly IGenerateService _generateService;
        private readonly ILanguageService _languageService;
        private readonly IAudioService _audioService;
        private readonly IUpscaleService _upscaleService;
        private readonly IExtractService _extractService;
        private readonly IInterpolationService _interpolationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelAccessService"/> class.
        /// </summary>
        /// <param name="generateService">The generate service.</param>
        /// <param name="languageService">The language service.</param>
        /// <param name="audioService">The audio service.</param>
        /// <param name="upscaleService">The upscale service.</param>
        /// <param name="extractService">The extract service.</param>
        /// <param name="interpolationService">The interpolation service.</param>
        /// <param name="logger">The logger.</param>
        public ModelAccessService(IGenerateService generateService, ILanguageService languageService, IAudioService audioService, IUpscaleService upscaleService, IExtractService extractService, IInterpolationService interpolationService, ILogger<ModelAccessService> logger)
        {
            _logger = logger;
            _generateService = generateService;
            _languageService = languageService;
            _audioService = audioService;
            _upscaleService = upscaleService;
            _extractService = extractService;
            _interpolationService = interpolationService;
        }


        /// <summary>
        /// Requests access to load the models of the pipeline, asking what to do with each model another pipeline has loaded.
        /// </summary>
        /// <param name="pipeline">The pipeline to load.</param>
        /// <returns><c>true</c> when the pipeline can be loaded, <c>false</c> when the load was canceled.</returns>
        public async Task<bool> RequestAccessAsync(PipelineModel pipeline)
        {
            var owner = GetOwner(pipeline);
            if (owner == ModelOwnerType.None || !IsLoadRequired(pipeline, owner))
                return true;

            return await ResolveOwnersAsync(pipeline, owner);
        }


        /// <summary>
        /// Requests access to load the interpolation model, asking what to do with each model another pipeline has loaded.
        /// </summary>
        /// <param name="pipeline">The interpolation pipeline, it has a device and no models.</param>
        /// <returns><c>true</c> when the interpolation model can be loaded, <c>false</c> when the load was canceled.</returns>
        public async Task<bool> RequestInterpolationAccessAsync(PipelineModel pipeline)
        {
            return await ResolveOwnersAsync(pipeline ?? new PipelineModel(), ModelOwnerType.Interpolation);
        }


        /// <summary>
        /// Requests the release of a model before its files change, e.g. it is deleted or another checkpoint set is downloaded,
        /// asking to unload every loaded pipeline that uses it.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns><c>true</c> when no loaded pipeline uses the model, <c>false</c> when the user canceled.</returns>
        public async Task<bool> RequestModelReleaseAsync(IDownloadModel model)
        {
            if (model is null)
                return true;

            return await ReleaseAsync(AppDefault.ModelReleaseMessage, x => x.IsUsing(model));
        }


        /// <summary>
        /// Requests the release of every pipeline that runs from the environment before it is installed, updated or deleted.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="loadingPipeline">The pipeline that is loading and needs the environment, the models its view has loaded are unloaded without asking and the view reloads them.</param>
        /// <returns><c>true</c> when no loaded pipeline runs on the environment's backend, <c>false</c> when the user canceled.</returns>
        public async Task<bool> RequestEnvironmentReleaseAsync(EnvironmentModel environment, PipelineModel loadingPipeline = null)
        {
            if (environment is null)
                return true;

            return await ReleaseAsync(AppDefault.EnvironmentReleaseMessage, x => x.Backends.Contains(environment.Backend), loadingPipeline);
        }


        /// <summary>
        /// Requests the release of every loaded pipeline, e.g. before the model folder moves.
        /// </summary>
        /// <returns><c>true</c> when nothing is loaded, <c>false</c> when the user canceled.</returns>
        public async Task<bool> RequestReleaseAllAsync()
        {
            return await ReleaseAsync(AppDefault.FolderReleaseMessage, x => true);
        }


        /// <summary>
        /// Gets the views that have a pipeline loaded or loading.
        /// </summary>
        public IReadOnlyList<View> GetLoadedViews()
        {
            return GetLoadedPipelines()
                .Where(x => x.ViewType.HasValue)
                .Select(x => x.ViewType.Value)
                .Distinct()
                .ToList();
        }


        /// <summary>
        /// Asks what to do with the models of every other kind of pipeline before the pipeline loads, a busy one cancels the load.
        /// The upscaler or extractor a view loaded is kept without asking when the pipeline uses the same one.
        /// A single loaded pipeline is asked about on its own, several are asked about together as a list.
        /// </summary>
        /// <param name="pipeline">The pipeline to load.</param>
        /// <param name="owner">The kind of the pipeline.</param>
        private async Task<bool> ResolveOwnersAsync(PipelineModel pipeline, ModelOwnerType owner)
        {
            var loadedPipelines = GetLoadedPipelines()
                .Where(x => x.Owner != owner && !IsLoadedModelKept(x.Owner, pipeline))
                .ToList();
            if (loadedPipelines.Count == 0)
                return true;

            if (loadedPipelines.Any(x => x.IsBusy))
                return await ShowBusyAsync();

            if (loadedPipelines.Count == 1)
                return await ResolveAsync(loadedPipelines[0], GetLoadedMessage(loadedPipelines[0].Owner, pipeline, owner).Detail);

            return await ResolveListAsync(loadedPipelines, pipeline, owner);
        }


        /// <summary>
        /// Determines whether the loaded pipeline is the upscaler or extractor of a view that the pipeline to load uses too, it is kept as it is.
        /// </summary>
        /// <param name="loadedOwner">The kind of the loaded pipeline.</param>
        /// <param name="pipeline">The pipeline to load.</param>
        private bool IsLoadedModelKept(ModelOwnerType loadedOwner, PipelineModel pipeline)
        {
            return (loadedOwner == ModelOwnerType.Upscale && IsUpscaleModelLoaded(pipeline))
                || (loadedOwner == ModelOwnerType.Extract && IsExtractModelLoaded(pipeline));
        }


        /// <summary>
        /// Gets the messages that say what loading the pipeline does with a loaded pipeline, a model the pipeline to load replaces says so.
        /// </summary>
        /// <param name="loadedOwner">The kind of the loaded pipeline.</param>
        /// <param name="pipeline">The pipeline to load.</param>
        /// <param name="owner">The kind of the pipeline to load.</param>
        /// <returns>The message format that asks about the loaded pipeline on its own, its name is the argument, and its note in the list of loaded pipelines, <c>null</c> when nothing of it is replaced.</returns>
        private (string Detail, string Note) GetLoadedMessage(ModelOwnerType loadedOwner, PipelineModel pipeline, ModelOwnerType owner)
        {
            switch (loadedOwner)
            {
                case ModelOwnerType.Diffusion when owner == ModelOwnerType.Upscale && GetUpscaleOwner() == ModelOwnerType.Diffusion:
                case ModelOwnerType.Diffusion when owner == ModelOwnerType.Extract && GetExtractOwner() == ModelOwnerType.Diffusion:
                    return (AppDefault.ModelPipelineReplaceDetail, AppDefault.ModelPipelineReplaceNote);
                case ModelOwnerType.Upscale when pipeline.UpscaleModel is not null:
                case ModelOwnerType.Extract when pipeline.ExtractModel is not null:
                    return (AppDefault.ModelViewReplaceDetail, AppDefault.ModelViewReplaceNote);
                default:
                    return (AppDefault.ModelLoadedDetail, null);
            }
        }


        /// <summary>
        /// Asks to unload the loaded pipelines that match, a loaded model cannot stay loaded while its files or environment change.
        /// </summary>
        /// <param name="messageFormat">The message format, the names of the loaded pipelines are its argument.</param>
        /// <param name="predicate">Selects the loaded pipelines that must be unloaded.</param>
        /// <param name="loadingPipeline">The pipeline that is loading, the loaded pipeline of its view is unloaded without asking and is not busy.</param>
        /// <returns><c>true</c> when no loaded pipeline matches or all were unloaded.</returns>
        private async Task<bool> ReleaseAsync(string messageFormat, Func<LoadedPipeline, bool> predicate, PipelineModel loadingPipeline = null)
        {
            var loadedPipelines = GetLoadedPipelines().Where(predicate).ToList();
            if (loadedPipelines.Count == 0)
                return true;

            var ownPipelines = loadedPipelines.Where(x => loadingPipeline is not null && x.ViewType == loadingPipeline.ViewType).ToList();
            var otherPipelines = loadedPipelines.Except(ownPipelines).ToList();
            if (otherPipelines.Any(x => x.IsBusy))
                return await ShowBusyAsync();

            var names = string.Join("', '", loadedPipelines.Select(x => x.Name));
            if (otherPipelines.Count > 0)
            {
                var otherNames = string.Join("', '", otherPipelines.Select(x => x.Name));
                if (!await DialogService.ShowMessageAsync(AppDefault.ModelLoadedTitle, string.Format(messageFormat, otherNames), MessageDialogType.YesNo, MessageBoxIconType.Question, MessageBoxStyleType.Info))
                {
                    _logger.LogInformation("[ModelAccess] [Release] Unload declined for {Names}", otherNames);
                    return false;
                }
            }

            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                foreach (var loadedPipeline in loadedPipelines)
                    await loadedPipeline.UnloadAsync();

                _logger.LogInformation("[ModelAccess] [Release] Unloaded {Names}, Own: {OwnCount}, Elapsed: {Elapsed:c}", names, ownPipelines.Count, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelAccess] [Release] An exception occurred unloading {Names}, Elapsed: {Elapsed:c}", names, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.UnloadPipelineTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Gets every loaded pipeline with the models, components and backends it uses, in the order they are asked about.
        /// A pipeline that is loading is busy, its unload leaves the models that are still loading to the load that owns them.
        /// </summary>
        private List<LoadedPipeline> GetLoadedPipelines()
        {
            var loadedPipelines = new List<LoadedPipeline>();
            var upscaleOwner = GetUpscaleOwner();
            var extractOwner = GetExtractOwner();
            var isUpscaleOwned = upscaleOwner == ModelOwnerType.Diffusion;
            var isExtractOwned = extractOwner == ModelOwnerType.Diffusion;
            var isGenerateActive = _generateService.IsLoaded || _generateService.IsLoading;
            if (isGenerateActive || isUpscaleOwned || isExtractOwned)
            {
                var pipeline = GetDiffusionPipeline(isUpscaleOwned, isExtractOwned);
                var diffusionPipeline = new LoadedPipeline(ModelOwnerType.Diffusion, pipeline?.DiffusionModel?.Name, pipeline?.ViewType, pipeline?.Device, () => IsDiffusionPipelineBusy(isUpscaleOwned, isExtractOwned), () => UnloadDiffusionPipelineAsync(isUpscaleOwned, isExtractOwned));
                if (isGenerateActive)
                    diffusionPipeline.AddGenerate(_generateService.Pipeline);

                if (isUpscaleOwned)
                    diffusionPipeline.AddModel(_upscaleService.Pipeline?.UpscaleModel, _upscaleService.Pipeline?.UpscaleModel?.Backend);

                if (isExtractOwned)
                    diffusionPipeline.AddModel(_extractService.Pipeline?.ExtractModel, _extractService.Pipeline?.ExtractModel?.Backend);

                loadedPipelines.Add(diffusionPipeline);
            }

            AddGeneratePipeline(loadedPipelines, ModelOwnerType.Language, _languageService);
            AddGeneratePipeline(loadedPipelines, ModelOwnerType.Audio, _audioService);

            if (upscaleOwner == ModelOwnerType.Upscale)
            {
                var upscaleModel = _upscaleService.Pipeline?.UpscaleModel;
                var upscalePipeline = new LoadedPipeline(ModelOwnerType.Upscale, upscaleModel?.Name, _upscaleService.Pipeline?.ViewType, _upscaleService.Pipeline?.Device, () => _upscaleService.IsLoading || _upscaleService.IsExecuting, () => _upscaleService.IsLoaded ? _upscaleService.UnloadAsync() : Task.CompletedTask);
                upscalePipeline.AddModel(upscaleModel, upscaleModel?.Backend);
                loadedPipelines.Add(upscalePipeline);
            }

            if (extractOwner == ModelOwnerType.Extract)
            {
                var extractModel = _extractService.Pipeline?.ExtractModel;
                var extractPipeline = new LoadedPipeline(ModelOwnerType.Extract, extractModel?.Name, _extractService.Pipeline?.ViewType, _extractService.Pipeline?.Device, () => _extractService.IsLoading || _extractService.IsExecuting, () => _extractService.IsLoaded ? _extractService.UnloadAsync() : Task.CompletedTask);
                extractPipeline.AddModel(extractModel, extractModel?.Backend);
                loadedPipelines.Add(extractPipeline);
            }

            if (_interpolationService.IsLoaded || _interpolationService.IsLoading)
            {
                var interpolationPipeline = new LoadedPipeline(ModelOwnerType.Interpolation, AppDefault.Menu_VideoInterpolate, View.VideoInterpolate, null, () => _interpolationService.IsLoading || _interpolationService.IsExecuting, () => _interpolationService.IsLoaded ? _interpolationService.UnloadAsync() : Task.CompletedTask);
                interpolationPipeline.AddModel(null, BackendType.OnnxRuntime);
                loadedPipelines.Add(interpolationPipeline);
            }

            return loadedPipelines;
        }


        /// <summary>
        /// Adds the pipeline of a service that runs a single generate model in its own host, the language and audio services, when it is loaded or loading.
        /// </summary>
        /// <param name="loadedPipelines">The loaded pipelines.</param>
        /// <param name="owner">The kind of pipeline the service runs.</param>
        /// <param name="generateService">The service.</param>
        private static void AddGeneratePipeline(List<LoadedPipeline> loadedPipelines, ModelOwnerType owner, IGenerateService generateService)
        {
            if (!generateService.IsLoaded && !generateService.IsLoading)
                return;

            var pipeline = generateService.Pipeline;
            var loadedPipeline = new LoadedPipeline(owner, pipeline?.GenerateModel?.Name, pipeline?.ViewType, pipeline?.Device, () => generateService.IsLoading || generateService.IsExecuting, () => generateService.IsLoaded ? generateService.UnloadAsync() : Task.CompletedTask);
            loadedPipeline.AddGenerate(pipeline);
            loadedPipelines.Add(loadedPipeline);
        }


        /// <summary>
        /// Gets the pipeline that names the loaded diffusion pipeline and its view, the diffusion model's, a diffusion model that is loading
        /// already has its pipeline, or the upscaler's or extractor's when only that is loaded.
        /// </summary>
        /// <param name="isUpscaleOwned">Whether the loaded upscaler belongs to the diffusion pipeline.</param>
        /// <param name="isExtractOwned">Whether the loaded extractor belongs to the diffusion pipeline.</param>
        private PipelineModel GetDiffusionPipeline(bool isUpscaleOwned, bool isExtractOwned)
        {
            if ((_generateService.IsLoaded || _generateService.IsLoading) && _generateService.Pipeline is not null)
                return _generateService.Pipeline;

            if (isUpscaleOwned && _upscaleService.Pipeline is not null)
                return _upscaleService.Pipeline;

            return isExtractOwned ? _extractService.Pipeline : null;
        }


        /// <summary>
        /// Gets the kind of pipeline that loaded the upscale model, it is reloaded during an execute when its tile size changes.
        /// </summary>
        public ModelOwnerType GetUpscaleOwner()
        {
            if (!_upscaleService.IsLoaded && !_upscaleService.IsLoading && !_upscaleService.IsExecuting)
                return ModelOwnerType.None;

            return IsDiffusionPipeline(_upscaleService.Pipeline) ? ModelOwnerType.Diffusion : ModelOwnerType.Upscale;
        }


        /// <summary>
        /// Gets the kind of pipeline that loaded the extract model.
        /// </summary>
        public ModelOwnerType GetExtractOwner()
        {
            if (!_extractService.IsLoaded && !_extractService.IsLoading)
                return ModelOwnerType.None;

            return IsDiffusionPipeline(_extractService.Pipeline) ? ModelOwnerType.Diffusion : ModelOwnerType.Extract;
        }


        /// <summary>
        /// Determines whether the upscale model of the pipeline is the one loaded, on the pipeline's device.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public bool IsUpscaleModelLoaded(PipelineModel pipeline)
        {
            return _upscaleService.IsLoaded
                && pipeline?.UpscaleModel is not null
                && _upscaleService.Pipeline?.UpscaleModel == pipeline.UpscaleModel
                && _upscaleService.Pipeline?.Device == pipeline.Device;
        }


        /// <summary>
        /// Determines whether the extract model of the pipeline is the one loaded.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public bool IsExtractModelLoaded(PipelineModel pipeline)
        {
            return _extractService.IsLoaded
                && pipeline?.ExtractModel is not null
                && _extractService.Pipeline?.ExtractModel == pipeline.ExtractModel;
        }


        /// <summary>
        /// Gets the kind of the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private static ModelOwnerType GetOwner(PipelineModel pipeline)
        {
            if (pipeline is null)
                return ModelOwnerType.None;

            if (pipeline.DiffusionModel is not null)
                return pipeline.DiffusionModel.Backend == BackendType.AudioCpp ? ModelOwnerType.Audio : ModelOwnerType.Diffusion;

            if (pipeline.LanguageModel is not null)
                return ModelOwnerType.Language;

            if (pipeline.UpscaleModel is not null)
                return ModelOwnerType.Upscale;

            if (pipeline.ExtractModel is not null)
                return ModelOwnerType.Extract;

            return ModelOwnerType.None;
        }


        /// <summary>
        /// Determines whether loading the pipeline loads a model, a reload or update of the loaded models needs no access.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="owner">The kind of the pipeline.</param>
        private bool IsLoadRequired(PipelineModel pipeline, ModelOwnerType owner)
        {
            if (owner == ModelOwnerType.Language)
                return !_languageService.IsLoaded || _languageService.Pipeline.IsLoadRequired(pipeline);

            if (owner == ModelOwnerType.Audio)
                return !_audioService.IsLoaded || _audioService.Pipeline.IsLoadRequired(pipeline);

            if (owner != ModelOwnerType.Diffusion)
                return true;

            return !_generateService.IsLoaded
                || _generateService.Pipeline.IsLoadRequired(pipeline)
                || (pipeline.UpscaleModel is not null && !IsUpscaleModelLoaded(pipeline))
                || (pipeline.ExtractModel is not null && !IsExtractModelLoaded(pipeline));
        }


        /// <summary>
        /// Determines whether the diffusion model, or the upscaler or extractor loaded with it, is loading or executing.
        /// </summary>
        /// <param name="isUpscaleOwned">Whether the loaded upscaler belongs to the diffusion pipeline.</param>
        /// <param name="isExtractOwned">Whether the loaded extractor belongs to the diffusion pipeline.</param>
        private bool IsDiffusionPipelineBusy(bool isUpscaleOwned, bool isExtractOwned)
        {
            return _generateService.IsLoading
                || _generateService.IsExecuting
                || (isUpscaleOwned && (_upscaleService.IsLoading || _upscaleService.IsExecuting))
                || (isExtractOwned && (_extractService.IsLoading || _extractService.IsExecuting));
        }


        /// <summary>
        /// Shows the loaded model dialog and unloads the model when the user chooses to.
        /// </summary>
        /// <param name="loadedPipeline">The loaded pipeline.</param>
        /// <param name="messageFormat">The message format, the model name is its argument.</param>
        /// <returns><c>true</c> when the load can continue.</returns>
        private async Task<bool> ResolveAsync(LoadedPipeline loadedPipeline, string messageFormat)
        {
            var dialog = DialogService.GetDialog<ModelLoadedDialog>();
            var result = await dialog.ShowDialogAsync(string.Format(messageFormat, loadedPipeline.Name));
            _logger.LogInformation("[ModelAccess] [RequestAccess] Model {Name} is loaded, Result: {Result}", loadedPipeline.Name, result);
            if (result != ModelLoadedResult.Unload)
                return result == ModelLoadedResult.Continue;

            return await UnloadAsync(loadedPipeline);
        }


        /// <summary>
        /// Shows the loaded pipelines as a list, each can be unloaded from the list before the load continues with the ones left loaded.
        /// The memory of the device the pipeline loads on is shown with them, and of the devices the loaded pipelines are on,
        /// with more than one device each loaded pipeline names its device.
        /// </summary>
        /// <param name="loadedPipelines">The loaded pipelines.</param>
        /// <param name="pipeline">The pipeline to load.</param>
        /// <param name="owner">The kind of the pipeline to load.</param>
        /// <returns><c>true</c> when the load can continue.</returns>
        private async Task<bool> ResolveListAsync(IReadOnlyList<LoadedPipeline> loadedPipelines, PipelineModel pipeline, ModelOwnerType owner)
        {
            var devices = loadedPipelines
                .Select(x => x.Device)
                .Prepend(pipeline.Device)
                .Where(x => x is not null)
                .Distinct()
                .ToList();
            var items = loadedPipelines
                .Select(x => new ModelLoadedItem(x.Name, GetViewName(x), devices.Count > 1 ? x.Device?.Name : null, GetLoadedMessage(x.Owner, pipeline, owner).Note, x.MemorySize, () => UnloadListAsync(x)))
                .ToList();

            var dialog = DialogService.GetDialog<ModelLoadedListDialog>();
            var result = await dialog.ShowDialogAsync(items, devices);
            var loadedNames = string.Join("', '", items.Where(x => x.Status != ModelLoadedStatus.Unloaded).Select(x => x.Name));
            _logger.LogInformation("[ModelAccess] [RequestAccess] {Count} models are loaded, Continue: {Result}, Left loaded: '{Names}'", items.Count, result, loadedNames);
            return result;
        }


        /// <summary>
        /// Unloads a pipeline from the list of loaded pipelines, one that started loading or executing since the list was shown cancels the load.
        /// </summary>
        /// <param name="loadedPipeline">The loaded pipeline.</param>
        /// <returns><c>true</c> when the pipeline was unloaded.</returns>
        private async Task<bool> UnloadListAsync(LoadedPipeline loadedPipeline)
        {
            if (loadedPipeline.IsBusy)
                return await ShowBusyAsync();

            return await UnloadAsync(loadedPipeline);
        }


        /// <summary>
        /// Unloads a loaded pipeline, an error is shown when it fails.
        /// </summary>
        /// <param name="loadedPipeline">The loaded pipeline.</param>
        /// <returns><c>true</c> when the pipeline was unloaded.</returns>
        private async Task<bool> UnloadAsync(LoadedPipeline loadedPipeline)
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                await loadedPipeline.UnloadAsync();
                _logger.LogInformation("[ModelAccess] [Unload] Model {Name} unloaded, Elapsed: {Elapsed:c}", loadedPipeline.Name, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelAccess] [Unload] An exception occurred unloading model {Name}, Elapsed: {Elapsed:c}", loadedPipeline.Name, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.UnloadPipelineTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Gets the name of the view that loaded the pipeline, as shown on its menu button.
        /// </summary>
        /// <param name="loadedPipeline">The loaded pipeline.</param>
        /// <returns>The name of the view, <c>null</c> when the view is not known or the pipeline is named after it.</returns>
        private static string GetViewName(LoadedPipeline loadedPipeline)
        {
            if (!loadedPipeline.ViewType.HasValue)
                return null;

            var viewName = AppDefault.ResourceManager.GetString($"Menu_{loadedPipeline.ViewType.Value}", AppDefault.Culture);
            return viewName == loadedPipeline.Name ? null : viewName;
        }


        /// <summary>
        /// Unloads the diffusion model, and the upscaler and extractor loaded with it.
        /// </summary>
        /// <param name="isUpscaleOwned">Whether the loaded upscaler belongs to the diffusion pipeline.</param>
        /// <param name="isExtractOwned">Whether the loaded extractor belongs to the diffusion pipeline.</param>
        private async Task UnloadDiffusionPipelineAsync(bool isUpscaleOwned, bool isExtractOwned)
        {
            if (isUpscaleOwned && _upscaleService.IsLoaded)
                await _upscaleService.UnloadAsync();

            if (isExtractOwned && _extractService.IsLoaded)
                await _extractService.UnloadAsync();

            if (_generateService.IsLoaded)
                await _generateService.UnloadAsync();
        }


        /// <summary>
        /// Shows that a loaded model is busy, the load is canceled.
        /// </summary>
        /// <returns><c>false</c>, the load is canceled.</returns>
        private async Task<bool> ShowBusyAsync()
        {
            _logger.LogInformation("[ModelAccess] [RequestAccess] Another model is loading or executing, load canceled");
            await DialogService.ShowMessageAsync(AppDefault.ModelBusyTitle, AppDefault.ModelBusyMessage, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
            return false;
        }


        /// <summary>
        /// Determines whether the pipeline is a diffusion pipeline, rather than an upscale or extract only pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private static bool IsDiffusionPipeline(PipelineModel pipeline)
        {
            return pipeline?.DiffusionModel is not null;
        }


        /// <summary>
        /// A loaded pipeline, the models, components and backends it uses, and how to unload it.
        /// </summary>
        private sealed class LoadedPipeline
        {
            private readonly Func<bool> _isBusy;
            private readonly Func<Task> _unloadAsync;
            private readonly List<IDownloadModel> _models = new List<IDownloadModel>();
            private readonly List<CheckpointComponent> _components = new List<CheckpointComponent>();
            private readonly List<BackendType> _backends = new List<BackendType>();

            /// <summary>
            /// Initializes a new instance of the <see cref="LoadedPipeline"/> class.
            /// </summary>
            /// <param name="owner">The kind of pipeline.</param>
            /// <param name="name">The name shown to the user.</param>
            /// <param name="viewType">The view that loaded the pipeline, or <c>null</c> when it is not known yet.</param>
            /// <param name="device">The device the pipeline is loaded on, or <c>null</c> when it is not known.</param>
            /// <param name="isBusy">Determines whether the pipeline is loading or executing, read when asked as dialogs are shown in between.</param>
            /// <param name="unloadAsync">The unload operation.</param>
            public LoadedPipeline(ModelOwnerType owner, string name, View? viewType, DeviceModel device, Func<bool> isBusy, Func<Task> unloadAsync)
            {
                Owner = owner;
                Name = name;
                ViewType = viewType;
                Device = device;
                _isBusy = isBusy;
                _unloadAsync = unloadAsync;
            }

            public ModelOwnerType Owner { get; }
            public string Name { get; }
            public View? ViewType { get; }
            public DeviceModel Device { get; }
            public float MemorySize { get; private set; }
            public bool IsBusy => _isBusy();
            public IReadOnlyList<BackendType> Backends => _backends;


            /// <summary>
            /// Adds the generate model of the pipeline, with its ControlNet, LoRA adapters and components, and the memory of the checkpoint set that is loaded.
            /// </summary>
            /// <param name="pipeline">The pipeline.</param>
            public void AddGenerate(PipelineModel pipeline)
            {
                var generateModel = pipeline?.GenerateModel;
                if (generateModel is null)
                    return;

                AddModel(generateModel, generateModel.Backend);
                AddModel(pipeline.ControlNetModel, null);
                foreach (var loraAdapter in pipeline.LoraAdapterModel ?? [])
                    AddModel(loraAdapter, null);

                _components.AddRange(generateModel.GetComponents());
                MemorySize += generateModel.GetMemorySize(pipeline.CheckpointType);
            }


            /// <summary>
            /// Adds a model and the backend it runs on, an upscaler with its components, an upscaler or extractor with its memory.
            /// </summary>
            /// <param name="model">The model, or <c>null</c> when only the backend is known.</param>
            /// <param name="backend">The backend, or <c>null</c> when the model runs on the pipeline's backend.</param>
            public void AddModel(IDownloadModel model, BackendType? backend)
            {
                if (model is not null)
                    _models.Add(model);

                if (model is UpscaleModel upscaleModel)
                {
                    _components.AddRange(upscaleModel.GetComponents());
                    MemorySize += upscaleModel.MemorySize;
                }

                if (model is ExtractModel extractModel)
                    MemorySize += extractModel.MemorySize;

                if (backend.HasValue && !_backends.Contains(backend.Value))
                    _backends.Add(backend.Value);
            }


            /// <summary>
            /// Determines whether the pipeline uses the model, a component is used when a loaded checkpoint references its key.
            /// </summary>
            /// <param name="model">The model.</param>
            public bool IsUsing(IDownloadModel model)
            {
                if (model is ComponentModel component)
                    return _components.Any(x => x.Type == CheckpointType.Component && x.Path == component.Key);

                return _models.Contains(model);
            }


            /// <summary>
            /// Unloads the pipeline.
            /// </summary>
            public Task UnloadAsync()
            {
                return _unloadAsync();
            }
        }
    }


    public interface IModelAccessService
    {
        Task<bool> RequestAccessAsync(PipelineModel pipeline);
        Task<bool> RequestInterpolationAccessAsync(PipelineModel pipeline);
        Task<bool> RequestModelReleaseAsync(IDownloadModel model);
        Task<bool> RequestEnvironmentReleaseAsync(EnvironmentModel environment, PipelineModel loadingPipeline = null);
        Task<bool> RequestReleaseAllAsync();
        IReadOnlyList<View> GetLoadedViews();
        ModelOwnerType GetUpscaleOwner();
        ModelOwnerType GetExtractOwner();
        bool IsUpscaleModelLoaded(PipelineModel pipeline);
        bool IsExtractModelLoaded(PipelineModel pipeline);
    }
}
