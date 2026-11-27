using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using TensorStack.Media.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
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

        public FrameToFrameView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IGenerateService generateService, ILanguageService languageService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, IMediaService mediaService, ILogger<FrameToFrameView> logger)
        : base(settings, navigationService, downloadService, modelAccessService, generateService, languageService, extractService, upscaleService, historyService, logger)
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
                ModelControl.SetPipeline(GenerateService.Pipeline);
        }


        /// <summary>
        /// On View Close
        /// </summary>
        public override async Task CloseAsync()
        {
            await ResultControl.StopAsync();
            await InputVideoControl.StopAsync();
            await base.CloseAsync();
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
                await ExecuteVideoFramesAsync(Options).SaveAsync(tempVideoStreamFile, audioSourceFile: _sourceVideo.SourceFile);
                var resultVideoStream = await VideoInputStream.CreateAsync(tempVideoStreamFile);

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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[FrameToFrame] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
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
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Image))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tempVideoStreamFile = MediaService.GetTempFile(MediaType.Video);
                    await ExecuteVideoFramesAsync(automationJob.GenerateOptions).SaveAsync(tempVideoStreamFile, audioSourceFile: _sourceVideo.SourceFile);
                    var resultVideoStream = await VideoInputStream.CreateAsync(tempVideoStreamFile);

                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultVideoStream
                        : await SaveHistoryAsync(automationJob.GenerateOptions, resultVideoStream);
                    CompareVideo = _sourceVideo;

                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[FrameToFrame] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecuteAutomationTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
                VideoFrameProgress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                ResultImage = default;
                SourceImage1 = default;
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Determines whether this process can execute, a source video is required.
        /// </summary>
        protected override bool CanExecute()
        {
            return base.CanExecute() && _sourceVideo is not null;
        }


        /// <summary>
        /// Execute diffusion for each video frame
        /// </summary>
        private async IAsyncEnumerable<VideoFrame> ExecuteVideoFramesAsync(GenerateInputOptions inputOptions)
        {
            await foreach (var videoFrame in _sourceVideo.GetAsync())
            {
                SourceImage1 = new ImageInput(videoFrame.Frame);

                // Options
                var options = inputOptions with { InputImages = GetInputTensors() };

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                ResultImage = await resultTensor.ToImageInputAsync();
                yield return new VideoFrame(videoFrame.Index, ResultImage, videoFrame.SourceFrameRate);
                VideoFrameProgress.Update(videoFrame.Index + 1, _sourceVideo.FrameCount, string.Format(AppDefault.VideoFrameProgress, videoFrame.Index, _sourceVideo.FrameCount));
            }
        }


        /// <summary>
        /// Gets the input tensors.
        /// </summary>
        private List<ImageTensor> GetInputTensors()
        {
            var inputImages = new List<ImageTensor>();
            if (Options.IsSource1Enabled)
                inputImages.AddIfNotNull(_sourceImage1);
            if (Options.IsSource2Enabled)
                inputImages.AddIfNotNull(_sourceImage2);
            if (Options.IsSource3Enabled)
                inputImages.AddIfNotNull(_sourceImage3);
            if (Options.IsSource4Enabled)
                inputImages.AddIfNotNull(_sourceImage4);

            return inputImages;
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(GenerateInputOptions options, VideoInputStream videoResult)
        {
            Logger.LogInformation($"[FrameToFrame] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoResult, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline?.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline?.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.FrameToFrame,
            });
            Logger.LogInformation($"[FrameToFrame] [SaveHistory] History saved.");
            return result;
        }

    }
}