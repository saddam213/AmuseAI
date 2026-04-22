using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Image;
using TensorStack.Python.Common;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageToImageView.xaml
    /// </summary>
    public partial class ImageToImageView : ViewBaseDiffusion
    {
        private ImageInput _sourceImage;
        private ImageInput _extractImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageToImageView"/> class.
        /// </summary>
        public ImageToImageView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageToImageView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.ImageToImage;

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
            Logger.LogInformation($"[ImageToImage] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Options
                var options = Options with { };
                if (CurrentPipeline.ProcessType == ProcessType.ImageToImage)
                    options.InputImages = [_sourceImage];
                else if (CurrentPipeline.ProcessType == ProcessType.ImageControlNet)
                    options.InputControlImages = [_sourceImage];

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = _sourceImage;

                // History
                await SaveHistoryAsync(options);
                Logger.LogInformation("[ImageToImage] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToImage] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageToImage] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[ImageToImage] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Loading Automations...");
                var automationJobs = await AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Image);
                AutomationProgress.Update(0, automationJobs.Count, $"Automation: {0}/{automationJobs.Count}");
                foreach (var automationJob in automationJobs)
                {
                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage = automationJob.InputImages[0];

                    // Images
                    if (CurrentPipeline.ProcessType == ProcessType.ImageToImage)
                        automationJob.DiffusionOptions.InputImages = [_sourceImage];
                    else if (CurrentPipeline.ProcessType == ProcessType.ImageControlNet)
                        automationJob.DiffusionOptions.InputControlImages = [_sourceImage];

                    // Diffusion
                    var resultTensor = await ExecuteImageDiffusionAsync(automationJob.DiffusionOptions);

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = SourceImage;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.DiffusionOptions);
                    }

                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJobs.Count, $"Automation: {automationJob.Id}/{automationJobs.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageToImage] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToImage] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageToImage] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        private async Task<ImageInput> SaveHistoryAsync(DiffusionInputOptions options)
        {
            Logger.LogInformation($"[ImageToImage] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                ControlNetModel = CurrentPipeline.ControlNetModel?.Name,
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                ExtractModel = CurrentPipeline.ExtractModel?.Name,
                ExtractorType = CurrentPipeline.ExtractModel?.Type,
                ExtractOptions = CurrentPipeline.ExtractModel is not null ? ExtractOptions : null,
                Source = View.ImageToImage,
            });
            Logger.LogInformation($"[ImageToImage] [SaveHistory] History saved.");
            return result;
        }
    }
}