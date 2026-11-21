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
using TensorStack.Media.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageToVideoView.xaml
    /// </summary>
    public partial class ImageToVideoView : ViewBaseDiffusion
    {
        private ImageInput _sourceImage1;
        private ImageInput _sourceImage2;
        private ImageInput _extractImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageToVideoView"/> class.
        /// </summary>
        public ImageToVideoView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageToVideoView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.ImageToVideo;

        /// <summary>
        /// Gets or sets the source image.
        /// </summary>
        public ImageInput SourceImage1
        {
            get { return _sourceImage1; }
            set { SetProperty(ref _sourceImage1, value); }
        }

        /// <summary>
        /// Gets or sets the source image.
        /// </summary>
        public ImageInput SourceImage2
        {
            get { return _sourceImage2; }
            set { SetProperty(ref _sourceImage2, value); }
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
            await base.CloseAsync();
        }


        /// <summary>
        /// Execute thge pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageToVideo] [Execute] Executing pipeline...");

            try
            {
                var previousVideo = ResultVideo;
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Options
                var options = Options with { InputImages = GetInputTensors() };

                // Execute
                var resultTensor = await ExecuteVideoDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultVideo = await SaveHistoryAsync(options, resultTensor);
                CompareVideo = previousVideo;

                Logger.LogInformation("[ImageToVideo] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToVideo] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToVideo] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
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
            Logger.LogInformation($"[ImageToVideo] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Image))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage1 = automationJob.InputImages[0];

                    // Options
                    var options = automationJob.GenerateOptions with { InputImages = GetInputTensors() };

                    // Diffusion
                    var resultTensor = await ExecuteVideoDiffusionAsync(options);

                    // Upscale
                    resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                    // Result
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(automationJob.GenerateOptions, resultTensor);
                    CompareImage = SourceImage1;

                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageToVideo] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToVideo] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToVideo] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecuteAutomationTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Unloads the pipeline
        /// </summary>
        protected override Task<bool> UnloadPipelineAsync()
        {
            _extractImage = null;
            return base.UnloadPipelineAsync();
        }


        /// <summary>
        /// Called when SourceImage changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="image">The image.</param>
        protected async void OnSourceImageChanged(object sender, ImageInput image)
        {
            try
            {
                if (CurrentPipeline?.ExtractModel == null)
                    return;

                IsViewBusy = true;
                if (_sourceImage1 == null || _extractImage == _sourceImage1)
                    return;

                _extractImage = await ExecuteImageExtractAsync(_sourceImage1);
                SourceImage1 = _extractImage;
            }
            finally
            {
                Progress.Clear();
                IsViewBusy = false;
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

            return inputImages;
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="videoInput">The video input.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(GenerateInputOptions options, VideoInputStream videoInput)
        {
            Logger.LogInformation("[ImageToVideo] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoInput, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = UpscaledModel?.Name,
                UpscaleOptions = UpscaledModel is not null ? UpscaleOptions : null,
                ExtractModel = CurrentPipeline.ExtractModel?.Name,
                ExtractorType = CurrentPipeline.ExtractModel?.Type,
                ExtractOptions = CurrentPipeline.ExtractModel is not null ? ExtractOptions : null,
                Source = View.ImageToVideo,
            });
            Logger.LogInformation("[ImageToVideo] [SaveHistory] History saved.");
            return result;
        }
    }
}