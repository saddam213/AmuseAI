using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.Image;
using TensorStack.Python.Common;
using TensorStack.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    public abstract class ViewBaseDiffusion : ViewBase
    {
        private bool _isPipelineLoaded;
        private PipelineModel _currentPipeline;
        private ImageInput _resultImage;
        private ImageInput _compareImage;
        private VideoInputStream _resultVideo;
        private VideoInputStream _compareVideo;
        private DiffusionInputOptions _options;
        private ExtractInputOptions _extractOptions;
        private UpscaleInputOptions _upscaleOptions;
        private AutomationOptions _automationOptions;
        private bool _isAutomating;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBaseDiffusion"/> class.
        /// </summary>
        public ViewBaseDiffusion(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            DiffusionService = diffusionService;
            ExtractService = extractService;
            UpscaleService = upscaleService;
            Statistics = new StatisticsModel(Dispatcher);
            ProgressCallback = new Progress<RunProgress>(OnProgress);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
            ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, CanExecute);
            ExecuteAutomationCommand = new AsyncRelayCommand(ExecuteAutomationAsync, CanExecuteAutomation);
            StopCommand = new AsyncRelayCommand(DiffusionService.StopAsync);
            PythonProgressCallback = new Progress<PipelineProgress>(OnProgress);
            AutomationProgress = new ProgressInfo();
        }

        /// <summary>
        /// Gets the diffusion service.
        /// </summary>
        public IDiffusionService DiffusionService { get; }

        /// <summary>
        /// Gets the extract service.
        /// </summary>
        public IExtractService ExtractService { get; }

        /// <summary>
        /// Gets the upscale service.
        /// </summary>
        public IUpscaleService UpscaleService { get; }

        /// <summary>
        /// Gets the statistics.
        /// </summary>
        public StatisticsModel Statistics { get; }

        /// <summary>
        /// Gets or sets the execute command.
        /// </summary>
        public AsyncRelayCommand ExecuteCommand { get; set; }

        /// <summary>
        /// Gets or sets the execute automation command.
        public AsyncRelayCommand ExecuteAutomationCommand { get; set; }

        /// <summary>
        /// Gets or sets the cancel command.
        /// </summary>
        public AsyncRelayCommand CancelCommand { get; set; }

        /// <summary>
        /// Gets or sets the stop command.
        /// </summary>
        public AsyncRelayCommand StopCommand { get; set; }

        /// <summary>
        /// Gets the progress callback.
        /// </summary>
        public IProgress<RunProgress> ProgressCallback { get; }

        /// <summary>
        /// Gets the python progress callback.
        /// </summary>
        protected IProgress<PipelineProgress> PythonProgressCallback { get; }

        /// <summary>
        /// Gets or sets the automation progress.
        /// </summary>
        public ProgressInfo AutomationProgress { get;}

        /// <summary>
        /// Gets or sets a value indicating whether this instance is pipeline loaded.
        /// </summary>
        public bool IsPipelineLoaded
        {
            get { return _isPipelineLoaded; }
            set { SetProperty(ref _isPipelineLoaded, value); }
        }

        /// <summary>
        /// Gets or sets the current pipeline.
        /// </summary>
        public PipelineModel CurrentPipeline
        {
            get { return _currentPipeline; }
            set { SetProperty(ref _currentPipeline, value); }
        }

        /// <summary>
        /// Gets or sets the result image.
        /// </summary>
        public ImageInput ResultImage
        {
            get { return _resultImage; }
            set { SetProperty(ref _resultImage, value); }
        }

        /// <summary>
        /// Gets or sets the compare image.
        /// </summary>
        public ImageInput CompareImage
        {
            get { return _compareImage; }
            set { SetProperty(ref _compareImage, value); }
        }

        /// <summary>
        /// Gets or sets the result video.
        /// </summary>
        public VideoInputStream ResultVideo
        {
            get { return _resultVideo; }
            set { SetProperty(ref _resultVideo, value); }
        }

        /// <summary>
        /// Gets or sets the compare video.
        /// </summary>
        public VideoInputStream CompareVideo
        {
            get { return _compareVideo; }
            set { SetProperty(ref _compareVideo, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public DiffusionInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }

        /// <summary>
        /// Gets or sets the extract options.
        /// </summary>
        public ExtractInputOptions ExtractOptions
        {
            get { return _extractOptions; }
            set { SetProperty(ref _extractOptions, value); }
        }

        /// <summary>
        /// Gets or sets the upscale options.
        /// </summary>
        public UpscaleInputOptions UpscaleOptions
        {
            get { return _upscaleOptions; }
            set { SetProperty(ref _upscaleOptions, value); }
        }

        /// <summary>
        /// Gets or sets the automation options.
        /// </summary>
        public AutomationOptions AutomationOptions
        {
            get { return _automationOptions; }
            set { SetProperty(ref _automationOptions, value); }
        }


        /// <summary>
        /// Gets or sets a value indicating whether this instance is automating.
        /// </summary>
        public bool IsAutomating
        {
            get { return _isAutomating; }
            set { SetProperty(ref _isAutomating, value); }
        }


        /// <summary>
        /// Executes the pipeline.
        /// </summary>
        protected abstract Task ExecuteAsync();


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        /// <returns>Task.</returns>
        protected abstract Task ExecuteAutomationAsync();


        /// <summary>
        ///  On View Open
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            IsPipelineLoaded = DiffusionService.IsLoaded && DiffusionService.Pipeline == CurrentPipeline;
            Logger.LogInformation("[{View}] [Open] View opened, IsPipelineLoaded: {IsPipelineLoaded}", ViewName, IsPipelineLoaded);
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected virtual async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [LoadPipeline] Loading pipeline...", ViewName);

            try
            {
                await LoadExtractModelAsync();
                await LoadDiffusionModelAsync();
                await LoadUpscaleModelAsync();
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[{View}] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[{View}] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Load Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Unload pipeline
        /// </summary>
        protected virtual async Task<bool> UnloadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [UnloadPipeline] Unloading pipeline...", ViewName);

            try
            {
                if (ExtractService.IsLoaded)
                {
                    await ExtractService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded extract model.", ViewName);
                }

                if (DiffusionService.IsLoaded)
                {
                    await DiffusionService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded diffusion model.", ViewName);
                }

                if (UpscaleService.IsLoaded)
                {
                    await UpscaleService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded upscale model.", ViewName);
                }

                Logger.LogInformation("[{View}] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Unload Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Determines whether this process can execute.
        /// </summary>
        protected virtual bool CanExecute()
        {
            return !DiffusionService.IsExecuting
                && !UpscaleService.IsExecuting
                && !ExtractService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected virtual bool CanExecuteAutomation()
        {
            return CanExecute();
        }


        /// <summary>
        /// Cancels the LoadPipeline or Execute processes.
        /// </summary>
        protected virtual async Task CancelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (UpscaleService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling upscale process...", ViewName);
                await UpscaleService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Upscale process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }

            if (ExtractService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling extract process...", ViewName);
                await ExtractService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Extract process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }

            if (DiffusionService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling diffusion process...", ViewName);
                await DiffusionService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Diffusion process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }
        }


        /// <summary>
        /// Determines whether this process can cancel.
        /// </summary>
        protected virtual bool CanCancel()
        {
            return DiffusionService.CanCancel
                || UpscaleService.CanCancel
                || ExtractService.CanCancel;
        }


        /// <summary>
        /// Load the Diffusion model
        /// </summary>
        private async Task<bool> LoadDiffusionModelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (CurrentPipeline.DiffusionModel is not null)
            {
                if (DiffusionService.IsLoaded)
                {
                    if (DiffusionService.Pipeline.IsLoadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadDiffusionModel] Loading diffusion model {Name}...", ViewName, CurrentPipeline.DiffusionModel.Name);
                        await DiffusionService.LoadAsync(CurrentPipeline, PythonProgressCallback);
                    }
                    else if (DiffusionService.Pipeline.IsReloadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadDiffusionModel] Reloading diffusion model {Name}...", ViewName, CurrentPipeline.DiffusionModel.Name);
                        await DiffusionService.ReloadAsync(CurrentPipeline, PythonProgressCallback);
                    }
                    else
                    {
                        await DiffusionService.UpdateAsync(CurrentPipeline);
                    }
                    return true;
                }

                Logger.LogInformation("[{View}] [LoadDiffusionModel] Loading diffusion model {Name}...", ViewName, CurrentPipeline.DiffusionModel.Name);
                await DiffusionService.LoadAsync(CurrentPipeline, PythonProgressCallback);
                Logger.LogInformation("[{View}] [LoadDiffusionModel] Successfully loaded diffusion model, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }

            await DiffusionService.UnloadAsync();
            Logger.LogInformation("[{View}] [LoadDiffusionModel] Unloaded diffusion model.", ViewName);
            return false;
        }


        /// <summary>
        /// Execute image diffusion
        /// </summary>
        /// <param name="options">The options.</param>
        protected async Task<ImageTensor> ExecuteImageDiffusionAsync(DiffusionInputOptions options)
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [ExecuteImageDiffusion] Executing diffusion...", ViewName);

            var resultTensor = await DiffusionService.GenerateImageAsync(options);

            Logger.LogInformation("[{View}] [ExecuteImageDiffusion] Diffusion complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return resultTensor;
        }


        /// <summary>
        /// Execute video diffusion
        /// </summary>
        /// <param name="options">The options.</param>
        protected async Task<VideoInputStream> ExecuteVideoDiffusionAsync(DiffusionInputOptions options)
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [ExecuteVideoDiffusion] Executing diffusion...", ViewName);

            var resultTensor = await DiffusionService.GenerateVideoAsync(options);

            Logger.LogInformation("[{View}] [ExecuteVideoDiffusion] Diffusion complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return resultTensor;
        }


        /// <summary>
        /// Load extract model
        /// </summary>
        private async Task<bool> LoadExtractModelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (CurrentPipeline.ExtractModel is not null)
            {
                if (!ExtractService.IsLoaded || ExtractService.Pipeline.ExtractModel != CurrentPipeline.ExtractModel)
                {
                    Logger.LogInformation("[{View}] [LoadExtractModel] Loading extract model {Name}...", ViewName, CurrentPipeline.ExtractModel.Name);
                    await ExtractService.LoadAsync(CurrentPipeline);
                }

                Logger.LogInformation("[{View}] [LoadExtractModel] Successfully loaded extract model, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }

            await ExtractService.UnloadAsync();
            Logger.LogInformation("[{View}] [LoadExtractModel] Unloaded extract model.", ViewName);
            return false;
        }


        /// <summary>
        /// Execute image extract
        /// </summary>
        /// <param name="imageInput">The image input.</param>
        protected async Task<ImageInput> ExecuteImageExtractAsync(ImageInput imageInput)
        {
            if (!ExtractService.IsLoaded)
                return imageInput;

            var timestamp = Stopwatch.GetTimestamp();
            Progress.Indeterminate("Extracting Image...");
            Logger.LogInformation("[{View}] [ExecuteImageExtract] Executing extract...", ViewName);

            var extractedImage = await ExtractService.ExecuteAsync(new ExtractImageRequest
            {
                Image = imageInput,
                Options = ExtractOptions
            }, ProgressCallback);

            Logger.LogInformation("[{View}] [ExecuteImageExtract] Extract complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return await extractedImage.ToImageInputAsync();
        }


        /// <summary>
        /// Execute video extract
        /// </summary>
        /// <param name="videoInput">The video input.</param>
        protected async Task<VideoInputStream> ExecuteVideoExtractAsync(VideoInputStream videoInput)
        {
            if (!ExtractService.IsLoaded)
                return videoInput;

            var timestamp = Stopwatch.GetTimestamp();
            Progress.Indeterminate("Extracting Video...");
            Logger.LogInformation("[{View}] [ExecuteVideoExtract] Executing extract...", ViewName);

            videoInput = await ExtractService.ExecuteAsync(new ExtractVideoRequest
            {
                VideoStream = videoInput,
                Options = ExtractOptions
            }, ProgressCallback);

            Logger.LogInformation("[{View}] [ExecuteVideoExtract] Extract complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return videoInput;
        }


        /// <summary>
        /// Load upscale model
        /// </summary>
        private async Task<bool> LoadUpscaleModelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (CurrentPipeline.UpscaleModel is not null)
            {
                if (!UpscaleService.IsLoaded || UpscaleService.Pipeline.UpscaleModel != CurrentPipeline.UpscaleModel)
                {
                    Logger.LogInformation("[{View}] [LoadUpscaleModel] Loading upscale model {Name}...", ViewName, CurrentPipeline.UpscaleModel.Name);
                    await UpscaleService.LoadAsync(CurrentPipeline);
                }

                Logger.LogInformation("[{View}] [LoadUpscaleModel] Successfully loaded upscale model, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }

            await UpscaleService.UnloadAsync();
            Logger.LogInformation("[{View}] [LoadUpscaleModel] Unloaded upscale model.", ViewName);
            return false;
        }


        /// <summary>
        /// Execute image upscale
        /// </summary>
        /// <param name="imageInput">The image input.</param>
        protected async Task<ImageTensor> ExecuteImageUpscaleAsync(ImageTensor imageInput)
        {
            if (!UpscaleService.IsLoaded)
                return imageInput;

            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [ExecuteImageUpscale] Executing upscale...", ViewName);

            Progress.Indeterminate("Upscaling Image...");
            imageInput = await UpscaleService.ExecuteAsync(new UpscaleImageRequest
            {
                Image = imageInput,
                Options = UpscaleOptions
            }, ProgressCallback);

            Logger.LogInformation("[{View}] [ExecuteImageUpscale] Upscale complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return imageInput;
        }


        /// <summary>
        /// Execute video upscale
        /// </summary>
        /// <param name="videoInput">The video input.</param>
        protected async Task<VideoInputStream> ExecuteVideoUpscaleAsync(VideoInputStream videoInput)
        {
            if (!UpscaleService.IsLoaded)
                return videoInput;

            var timestamp = Stopwatch.GetTimestamp();
            Progress.Indeterminate("Upscaling Video...");
            Logger.LogInformation("[{View}] [ExecuteVideoUpscale] Executing upscale...", ViewName);

            videoInput = await UpscaleService.ExecuteAsync(new UpscaleVideoRequest
            {
                VideoStream = videoInput,
                Options = UpscaleOptions
            }, ProgressCallback);

            Logger.LogInformation("[{View}] [ExecuteVideoUpscale] Upscale complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return videoInput;
        }


        /// <summary>
        /// Called when the selected Pipeline changes
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedPipelineChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;
                Logger.LogInformation("[{View}] [SelectedPipelineChanged] A new pipeline has been created.", ViewName);
                if (pipeline?.DiffusionModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate("Downloading Models...");
                    if (!await DownloadModels(pipeline))
                        return; // Canceled/Failed to download models

                    Progress.Indeterminate($"Loading {CurrentPipeline.DiffusionModel.Name}...");
                    if (!await LoadPipelineAsync())
                        return;// Canceled/Failed to load pipeline

                    IsPipelineLoaded = true;
                }
            }
            finally
            {
                Progress.Clear();
                Statistics.Clear();
            }
        }


        /// <summary>
        /// Called when progress is received from a C# pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected virtual void OnProgress(RunProgress progress)
        {
            if (progress.Maximum > 1)
                Progress.Update(progress.Value, progress.Maximum, $"Tile {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate("Rendering Image...");

            Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Max}, Elapsed: {Elapsed:c}", ViewName, progress.Value, progress.Maximum, progress.Elapsed);
        }


        /// <summary>
        /// Called when progress is received from a Python pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected virtual void OnProgress(PipelineProgress progress)
        {
            if (CurrentPipeline is null)
                return;

            if (progress.Key == "Download")
            {
                Progress.Update(progress.Value, progress.Maximum, $"Downloading {CurrentPipeline.DiffusionModel.Name} files ({progress.Message})...");
            }
            else if (progress.Key == "Generate")
            {
                if (progress.Subkey == "Step")
                {
                    Statistics.Update(progress);
                    Progress.Update(progress.Value, progress.Maximum, $"Step: {progress.Value}/{progress.Maximum}");
                    Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Maximum}, it/s: {IterationsPerSecond:N2}, s/it: {SecondsPerIteration:N2}", ViewName, progress.Value, progress.Maximum, progress.IterationsPerSecond, progress.SecondsPerIteration);
                }
                else
                {
                    if (string.IsNullOrEmpty(progress.Subkey))
                    {
                        Progress.Indeterminate(progress.Message);
                        Logger.LogDebug("[{View}] [OnProgress] {Message}", ViewName, progress.Message);
                    }
                    else
                    {
                        Progress.Indeterminate($"Step: {progress.Subkey}...");
                        Logger.LogDebug("[{View}] [OnProgress] Step: {Subkey}, it/s: {IterationsPerSecond:N2}, s/it: {SecondsPerIteration:N2}", ViewName, progress.Subkey, progress.IterationsPerSecond, progress.SecondsPerIteration);
                    }
                }

            }
        }
    }
}
