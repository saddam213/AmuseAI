using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoToVideoView.xaml
    /// </summary>
    public partial class VideoToVideoView : ViewBaseDiffusion
    {
        private VideoInputStream _sourceVideo;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoToVideoView"/> class.
        /// </summary>
        public VideoToVideoView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<VideoToVideoView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.VideoToVideo;

        /// <summary>
        /// Gets or sets the source video.
        /// </summary>
        public VideoInputStream SourceVideo
        {
            get { return _sourceVideo; }
            set { SetProperty(ref _sourceVideo, value); }
        }


        /// <summary>
        /// On View Open
        /// </summary>
        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            await base.OpenAsync(args);
            if (!IsPipelineLoaded)
                ModelControl.SetPipeline(DiffusionService.Pipeline);
        }


        /// <summary>
        /// Execute thge pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoToVideo] [Execute] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Frames
                var frames = await GetInputFrames().ToListAsync();

                // Options
                var options = Options with { };
                options.InputImages = frames;

                // Execute
                var resultTensor = await ExecuteVideoDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultVideo = await SaveHistoryAsync(options, resultTensor);
                CompareVideo = _sourceVideo;

                Logger.LogInformation("[VideoToVideo] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoToVideo] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[VideoToVideo] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[VideoToVideo] [ExecuteAutomation] Executing pipeline...");

            try
            {
                var previousImage = ResultImage;
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Frames
                var frames = await GetInputFrames().ToListAsync();

                AutomationProgress.Indeterminate($"Loading Automations...");
                var automationJobs = await AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Video);
                AutomationProgress.Update(0, automationJobs.Count, $"Automation: {0}/{automationJobs.Count}");
                foreach (var automationJob in automationJobs)
                {
                    // Source
                    if (!automationJob.VideoStreams.IsNullOrEmpty())
                        SourceVideo = automationJob.VideoStreams[0];

                    // Images
                    automationJob.DiffusionOptions.InputImages = await GetInputFrames().ToListAsync();

                    // Diffusion
                    var resultTensor = await ExecuteVideoDiffusionAsync(automationJob.DiffusionOptions);

                    // Upscale
                    resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                    // Result
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(automationJob.DiffusionOptions, resultTensor);

                    // Output
                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJobs.Count, $"Automation: {automationJob.Id}/{automationJobs.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[VideoToVideo] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoToVideo] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[VideoToVideo] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="videoInput">The video input.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(DiffusionInputOptions options, VideoInputStream videoInput)
        {
            Logger.LogInformation("[VideoToVideo] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoInput, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                ExtractModel = CurrentPipeline.ExtractModel?.Name,
                ExtractorType = CurrentPipeline.ExtractModel?.Type,
                ExtractOptions = CurrentPipeline.ExtractModel is not null ? ExtractOptions : null,
                Source = View.VideoToVideo
            });
            Logger.LogInformation("[VideoToVideo] [SaveHistory] History saved.");
            return result;
        }


        private async IAsyncEnumerable<ImageTensor> GetInputFrames()
        {
            Progress.Clear();
            await foreach (var sourceFrame in _sourceVideo.GetAsync(Options.Width, Options.Height, Options.FrameRate, TensorStack.Common.ResizeMode.Crop).Take(Options.Frames))
            {
                Progress.Update(sourceFrame.Index, Options.Frames, $"Processing Input Frame: {sourceFrame.Index}/{sourceFrame.Frame}");
                yield return sourceFrame.Frame;
            }
            Progress.Indeterminate("Encoding Video Frames...");
        }
    }
}