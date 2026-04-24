using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Image;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageInpaintView.xaml
    /// </summary>
    public partial class ImageInpaintView : ViewBaseDiffusion
    {
        private ImageInput _outputImage;
        private ImageInput _outputImageMask;
        private ImageInput _sourceImage;
        private ImageInput _sourceImageMask;

        public ImageInpaintView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageInpaintView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        public override View View => View.ImageInpaint;

        /// <summary>
        /// Gets or sets the source image.
        /// </summary>
        public ImageInput SourceImage
        {
            get { return _sourceImage; }
            set { SetProperty(ref _sourceImage, value); }
        }

        /// <summary>
        /// Gets or sets the source image mask.
        /// </summary>
        public ImageInput SourceImageMask
        {
            get { return _sourceImageMask; }
            set { SetProperty(ref _sourceImageMask, value); }
        }

        /// <summary>
        /// Gets or sets the output image.
        /// </summary>
        public ImageInput OutputImage
        {
            get { return _outputImage; }
            set { SetProperty(ref _outputImage, value); }
        }

        /// <summary>
        /// Gets or sets the output image mask.
        /// </summary>
        public ImageInput OutputImageMask
        {
            get { return _outputImageMask; }
            set { SetProperty(ref _outputImageMask, value); }
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
            Logger.LogInformation($"[ImageInpaint] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Options
                var options = Options with { };
                options.InputImages = [_outputImage, _outputImageMask];

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = _outputImage;

                // History
                await SaveHistoryAsync(options);
                Logger.LogInformation("[ImageInpaint] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageInpaint] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageInpaint] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[ImageInpaint] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Automation Started");
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Image))
                {
                    // Diffusion
                    var resultTensor = await ExecuteImageDiffusionAsync(automationJob.DiffusionOptions with
                    {
                        InputImages = [_outputImage, _outputImageMask]
                    });

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = _outputImage;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.DiffusionOptions);
                    }

                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageInpaint] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageInpaint] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageInpaint] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            return DiffusionService.IsLoaded
                && !DiffusionService.IsExecuting
                && _outputImage is not null
                && _outputImageMask is not null;
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
                if (_sourceImage == null)
                {
                    SourceImageMask = null;
                    return;
                }

                SourceImageMask = await ExecuteImageExtractAsync(_sourceImage);
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
            Logger.LogInformation($"[ImageInpaint] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                ExtractModel = CurrentPipeline.ExtractModel?.Name,
                ExtractorType = CurrentPipeline.ExtractModel?.Type,
                ExtractOptions = CurrentPipeline.ExtractModel is not null ? ExtractOptions : null,
                Source = View.ImageInpaint,
            });
            Logger.LogInformation($"[ImageInpaint] [SaveHistory] History saved.");
            return result;
        }
    }
}