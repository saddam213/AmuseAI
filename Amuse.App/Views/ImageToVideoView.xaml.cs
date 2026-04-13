using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Image;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageToVideoView.xaml
    /// </summary>
    public partial class ImageToVideoView : ViewBaseDiffusion
    {
        private ImageInput _sourceImage;
        private ImageInput _extractImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageToVideoView"/> class.
        /// </summary>
        public ImageToVideoView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageToVideoView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
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
        public ImageInput SourceImage
        {
            get { return _sourceImage; }
            set { SetProperty(ref _sourceImage, value); }
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
                var options = Options with { };
                options.InputImages = [_sourceImage];

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
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageToVideo] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
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
                if (_sourceImage == null || _extractImage == _sourceImage)
                    return;

                _extractImage = await ExecuteImageExtractAsync(_sourceImage);
                SourceImage = _extractImage;
            }
            finally
            {
                Progress.Clear();
                IsViewBusy = false;
            }
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="videoInput">The video input.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(DiffusionInputOptions options, VideoInputStream videoInput)
        {
            Logger.LogInformation("[ImageToVideo] [SaveHistory] Saving history...");
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
                Source = View.ImageToVideo,
            });
            Logger.LogInformation("[ImageToVideo] [SaveHistory] History saved.");
            return result;
        }
    }
}