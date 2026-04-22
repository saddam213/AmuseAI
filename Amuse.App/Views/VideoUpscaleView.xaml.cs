using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoUpscaleView.xaml
    /// </summary>
    public partial class VideoUpscaleView : ViewBaseModel
    {
        private VideoInputStream _sourceVideo;
        private VideoInputStream _resultVideo;
        private VideoInputStream _compareVideo;
        private UpscaleInputOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoUpscaleView"/> class.
        /// </summary>
        public VideoUpscaleView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, IUpscaleService upscaleService, ILogger<VideoUpscaleView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            UpscaleService = upscaleService;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.VideoUpscale;

        /// <summary>
        /// Gets the upscale service.
        /// </summary>
        public IUpscaleService UpscaleService { get; }

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
        public UpscaleInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }

        /// <summary>
        /// Gets or sets the source video.
        /// </summary>
        public VideoInputStream SourceVideo
        {
            get { return _sourceVideo; }
            set { SetProperty(ref _sourceVideo, value); }
        }


        /// <summary>
        /// On view opened
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            ModelControl.SetPipeline(UpscaleService.Pipeline);
            IsPipelineLoaded = UpscaleService.IsLoaded && UpscaleService.Pipeline.UpscaleModel == CurrentPipeline?.UpscaleModel;
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoUpscale] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"Loading {CurrentPipeline.UpscaleModel.Name}...");

                await UpscaleService.LoadAsync(CurrentPipeline);
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[VideoUpscale] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[VideoUpscale] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoUpscale] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Load Pipeline", ex.Message);
                return false;
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Unload pipeline
        /// </summary>
        protected override async Task<bool> UnloadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoUpscale] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await UpscaleService.UnloadAsync();
                Logger.LogInformation("[VideoUpscale] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoUpscale] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Unload Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoUpscale] [Execute] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Upscaler
                var resultVideo = await UpscaleService.ExecuteAsync(new UpscaleVideoRequest
                {
                    VideoStream = _sourceVideo,
                    Options = _options
                }, ProgressCallback);

                Statistics.Stop();

                // Result
                ResultVideo = await HistoryService.AddAsync(resultVideo, new UpscaleHistory
                {
                    Options = _options,
                    Model = CurrentPipeline.UpscaleModel.Name,
                    Source = View.VideoUpscale,
                    OriginalWidth = _sourceVideo.Width,
                    OriginalHeight = _sourceVideo.Height,
                    ScaleFactor = CurrentPipeline.UpscaleModel.ScaleFactor
                });
                CompareVideo = _sourceVideo;

                Logger.LogInformation("[VideoUpscale] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoUpscale] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = UpscaleService.IsLoaded;
                Logger.LogError(ex, "[VideoUpscale] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        protected override async Task ExecuteAutomationAsync()
        {
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoUpscale] [ExecuteAutomation] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Loading Automations...");
                var automationJobs = await AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Video);
                AutomationProgress.Update(0, automationJobs.Count, $"Automation: {0}/{automationJobs.Count}");
                foreach (var automationJob in automationJobs)
                {
                    // Source
                    SourceVideo = automationJob.VideoStreams[0];

                    // Upscale
                    var resultVideo = await UpscaleService.ExecuteAsync(new UpscaleVideoRequest
                    {
                        VideoStream = _sourceVideo,
                        Options = automationJob.UpscaleOptions
                    }, ProgressCallback);

                    // Result
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultVideo
                        : await HistoryService.AddAsync(resultVideo, new UpscaleHistory
                        {
                            Options = automationJob.UpscaleOptions,
                            Model = CurrentPipeline.UpscaleModel.Name,
                            Source = View.VideoUpscale,
                            OriginalWidth = _sourceVideo.Width,
                            OriginalHeight = _sourceVideo.Height,
                            ScaleFactor = CurrentPipeline.UpscaleModel.ScaleFactor
                        });

                    CompareVideo = _sourceVideo;

                    // Output
                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJobs.Count, $"Automation: {automationJob.Id}/{automationJobs.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[VideoUpscale] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoUpscale] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = UpscaleService.IsLoaded;
                Logger.LogError(ex, "[VideoUpscale] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Automation", ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
            }
        }


        /// <summary>
        /// Determines whether this instance can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return _sourceVideo is not null && UpscaleService.IsLoaded && !UpscaleService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            return UpscaleService.IsLoaded && !UpscaleService.IsExecuting && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await UpscaleService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return UpscaleService.CanCancel;
        }


        /// <summary>
        /// Called when Upscale model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedUpscalerChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;
                if (pipeline?.UpscaleModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"Loading {pipeline.UpscaleModel.Name}...");

                    if (!await DownloadModels(pipeline))
                        return; // Canceled/Failed to download models

                    if (!await LoadPipelineAsync())
                        return; // Canceled/Failed to load pipeline

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
        protected override void OnProgress(RunProgress progress)
        {
            if (progress.Maximum > 1)
                Progress.Update(progress.Value, progress.Maximum, $"Frame {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate("Rendering Video...");

            Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Max}, Elapsed: {Elapsed:c}", ViewName, progress.Value, progress.Maximum, progress.Elapsed);
        }
    }
}