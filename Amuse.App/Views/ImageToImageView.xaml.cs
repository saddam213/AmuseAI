using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
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
        public ImageToImageView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageToImageView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
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
                ModelControl.SetPipeline(GenerateService.Pipeline);
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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToImage] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
                PreviewImage = default;
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
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Image))
                {
                    PreviewImage = default;
                    cancellationToken.ThrowIfCancellationRequested();

                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage = automationJob.InputImages[0];

                    // Images
                    if (CurrentPipeline.ProcessType == ProcessType.ImageToImage)
                        automationJob.GenerateOptions.InputImages = [_sourceImage];
                    else if (CurrentPipeline.ProcessType == ProcessType.ImageControlNet)
                        automationJob.GenerateOptions.InputControlImages = [_sourceImage];

                    // Diffusion
                    var resultTensor = await ExecuteImageDiffusionAsync(automationJob.GenerateOptions);

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = SourceImage;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.GenerateOptions);
                    }

                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToImage] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecuteAutomationTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = null;
                PreviewImage = default;
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
        private async Task<ImageInput> SaveHistoryAsync(GenerateInputOptions options)
        {
            Logger.LogInformation($"[ImageToImage] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                ControlNetModel = CurrentPipeline.ControlNetModel?.Name,
                UpscaleModel = UpscaledModel?.Name,
                UpscaleOptions = UpscaledModel is not null ? UpscaleOptions : null,
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