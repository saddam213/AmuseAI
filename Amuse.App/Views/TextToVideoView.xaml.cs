using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for TextToVideoView.xaml
    /// </summary>
    public partial class TextToVideoView : ViewBaseDiffusion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextToVideoView"/> class.
        /// </summary>
        public TextToVideoView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<TextToVideoView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextToVideo;


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
            Logger.LogInformation("[TextToVideo] [Execute] Executing pipeline...");

            try
            {
                var previousVideo = ResultVideo;
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Diffusion
                var options = Options with { };
                var resultTensor = await ExecuteVideoDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultVideo = await SaveHistoryAsync(options, resultTensor);
                CompareVideo = previousVideo;

                Logger.LogInformation("[TextToVideo] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToVideo] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[TextToVideo] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="videoInput">The video input.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(DiffusionInputOptions options, VideoInputStream videoInput)
        {
            Logger.LogInformation("[TextToVideo] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoInput, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.TextToVideo,
            });
            Logger.LogInformation("[TextToVideo] [SaveHistory] History saved.");
            return result;
        }

    }
}