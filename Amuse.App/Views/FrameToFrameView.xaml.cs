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
using TensorStack.Common.Video;
using TensorStack.Image;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for FrameToFrameView.xaml
    /// </summary>
    public partial class FrameToFrameView : ViewBaseDiffusion
    {
        private ImageInput _sourceImage1;
        private ImageInput _sourceImage2;
        private ImageInput _sourceImage3;
        private ImageInput _sourceImage4;
        private VideoInputStream _sourceVideo;

        public FrameToFrameView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, IMediaService mediaService, ILogger<FrameToFrameView> logger)
        : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            MediaService = mediaService;
            VideoFrameProgress = new ProgressInfo();
            InitializeComponent();
        }

        public override View View => View.FrameToFrame;
        public IMediaService MediaService { get; }
        public ProgressInfo VideoFrameProgress { get; }

        /// <summary>
        /// Gets or sets the source video.
        /// </summary>
        public VideoInputStream SourceVideo
        {
            get { return _sourceVideo; }
            set { SetProperty(ref _sourceVideo, value); }
        }

        /// <summary>
        /// Gets or sets the source image1.
        /// </summary>
        public ImageInput SourceImage1
        {
            get { return _sourceImage1; }
            set { SetProperty(ref _sourceImage1, value); }
        }

        /// <summary>
        /// Gets or sets the source image2.
        /// </summary>
        public ImageInput SourceImage2
        {
            get { return _sourceImage2; }
            set { SetProperty(ref _sourceImage2, value); }
        }

        /// <summary>
        /// Gets or sets the source image3.
        /// </summary>
        public ImageInput SourceImage3
        {
            get { return _sourceImage3; }
            set { SetProperty(ref _sourceImage3, value); }
        }

        /// <summary>
        /// Gets or sets the source image4.
        /// </summary>
        public ImageInput SourceImage4
        {
            get { return _sourceImage4; }
            set { SetProperty(ref _sourceImage4, value); }
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
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[FrameToFrame] [Execute] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                var previousImage = ResultImage;
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                var tempVideoStreamFile = MediaService.GetTempFile(MediaType.Video);
                var resultVideoStream = await MediaService.SaveWithAudioAsync(ExecuteVideoFramesAsync(), _sourceVideo.SourceFile, tempVideoStreamFile);

                Statistics.Stop();
                ResultVideo = await SaveHistoryAsync(Options, resultVideoStream);
                CompareVideo = _sourceVideo;

                Logger.LogInformation("[FrameToFrame] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[FrameToFrame] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[FrameToFrame] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                ResultImage = default;
                SourceImage1 = default;
            }
        }


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected override async Task ExecuteAutomationAsync()
        {
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[FrameToFrame] [ExecuteAutomation] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                var previousImage = ResultImage;
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Automation Started");
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Image))
                {
                    // Source
                    if (!automationJob.VideoStreams.IsNullOrEmpty())
                        SourceVideo = automationJob.VideoStreams[0];


                    var tempVideoStreamFile = MediaService.GetTempFile(MediaType.Video);
                    var resultVideoStream = await MediaService.SaveWithAudioAsync(ExecuteVideoFramesAsync(), _sourceVideo.SourceFile, tempVideoStreamFile);

                    Statistics.Stop();
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultVideoStream
                        : await SaveHistoryAsync(Options, resultVideoStream);
                    CompareVideo = _sourceVideo;

                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[FrameToFrame] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[FrameToFrame] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[FrameToFrame] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Automation", ex.Message);
            }
            finally
            {
                Progress.Clear();
                VideoFrameProgress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                ResultImage = default;
                SourceImage1 = default;
            }
        }


        /// <summary>
        /// Execute diffusion for each video frame
        /// </summary>
        private async IAsyncEnumerable<VideoFrame> ExecuteVideoFramesAsync()
        {
            await foreach (var videoFrame in _sourceVideo.GetAsync())
            {
                SourceImage1 = new ImageInput(videoFrame.Frame);

                // Images
                var inputImages = new List<ImageTensor>
                {
                    { _sourceImage1, Options.InputImageCount },
                    { _sourceImage2, Options.InputImageCount },
                    { _sourceImage3, Options.InputImageCount },
                    { _sourceImage4, Options.InputImageCount }
                };

                // Options
                var options = Options with
                {
                    InputImages = inputImages,
                    Prompt = Options.Prompt,
                };

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                ResultImage = await resultTensor.ToImageInputAsync();
                yield return new VideoFrame(videoFrame.Index, ResultImage, videoFrame.SourceFrameRate);
                VideoFrameProgress.Update(videoFrame.Index + 1, _sourceVideo.FrameCount, $"Video Frame: {videoFrame.Index}/{_sourceVideo.FrameCount}");
            }
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(DiffusionInputOptions options, VideoInputStream videoResult)
        {
            Logger.LogInformation($"[FrameToFrame] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoResult, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.FrameToFrame,
            });
            Logger.LogInformation($"[FrameToFrame] [SaveHistory] History saved.");
            return result;
        }









    }
}