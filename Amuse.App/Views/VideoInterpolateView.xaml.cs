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
    /// Interaction logic for VideoInterpolateView.xaml
    /// </summary>
    public partial class VideoInterpolateView : ViewBaseModel
    {
        private VideoInputStream _sourceVideo;
        private VideoInputStream _resultVideo;
        private VideoInputStream _compareVideo;
        private InterpolateInputOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoInterpolateView"/> class.
        /// </summary>
        public VideoInterpolateView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, IInterpolationService interpolationService, ILogger<VideoInterpolateView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            InterpolationService = interpolationService;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.VideoInterpolate;

        /// <summary>
        /// Gets the interpolation service.
        /// </summary>
        public IInterpolationService InterpolationService { get; }

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
        /// Gets or sets the source video.
        /// </summary>
        public VideoInputStream SourceVideo
        {
            get { return _sourceVideo; }
            set { SetProperty(ref _sourceVideo, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public InterpolateInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }


        /// <summary>
        /// On view opened
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            IsPipelineLoaded = InterpolationService.IsLoaded;
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoInterpolate] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate("Loading Pipeline...");

                await InterpolationService.LoadAsync(CurrentPipeline.Device);

                Logger.LogInformation("[VideoInterpolate] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[VideoInterpolate] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoInterpolate] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[VideoInterpolate] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await InterpolationService.UnloadAsync();
                Logger.LogInformation("[VideoInterpolate] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoInterpolate] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[VideoInterpolate] [Execute] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Interpolation
                var resultVideo = await InterpolationService.ExecuteAsync(new InterpolationRequest
                {
                    VideoStream = _sourceVideo,
                    Frames = _sourceVideo.FrameCount,
                    FrameRate = _sourceVideo.FrameRate,
                    Multiplier = _options.Multiplier
                }, ProgressCallback);

                Statistics.Stop();

                // Result
                ResultVideo = await HistoryService.AddAsync(resultVideo, new InterpolateHistory
                {
                    Multiplier = _options.Multiplier,
                    Source = View.VideoInterpolate,
                    FrameRate = resultVideo.FrameRate,
                    OriginalFrameRate = _sourceVideo.FrameRate
                });
                CompareVideo = _sourceVideo;

                Logger.LogInformation("[VideoInterpolate] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoInterpolate] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = InterpolationService.IsLoaded;
                Logger.LogError(ex, "[VideoInterpolate] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[VideoInterpolate] [ExecuteAutomation] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Automation Started");
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Video))
                {
                    // Source
                    SourceVideo = automationJob.VideoStreams[0];
                    var options = automationJob.InterpolateOptions;

                    // Interpolate
                    var resultVideo = await InterpolationService.ExecuteAsync(new InterpolationRequest
                    {
                        VideoStream = _sourceVideo,
                        Frames = _sourceVideo.FrameCount,
                        FrameRate = _sourceVideo.FrameRate,
                        Multiplier = options.Multiplier
                    }, ProgressCallback);

                    // Result
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultVideo
                        : await HistoryService.AddAsync(resultVideo, new InterpolateHistory
                        {
                            Multiplier = options.Multiplier,
                            Source = View.VideoInterpolate,
                            FrameRate = resultVideo.FrameRate,
                            OriginalFrameRate = _sourceVideo.FrameRate
                        });

                    CompareVideo = _sourceVideo;

                    // Output
                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[VideoInterpolate] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoInterpolate] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = InterpolationService.IsLoaded;
                Logger.LogError(ex, "[VideoInterpolate] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            return _sourceVideo is not null && InterpolationService.IsLoaded && !InterpolationService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            return InterpolationService.IsLoaded && !InterpolationService.IsExecuting && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            if (InterpolationService.IsLoading)
                CurrentPipeline = null;

            await InterpolationService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return InterpolationService.CanCancel;
        }


        /// <summary>
        /// Called when interpolation model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedInterpolationChanged(object sender, PipelineModel pipeline)
        {
            IsPipelineLoaded = false;
            CurrentPipeline = pipeline;
            if (pipeline == null)
            {
                await UnloadPipelineAsync();
            }
            else
            {
                IsPipelineLoaded = await LoadPipelineAsync();
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