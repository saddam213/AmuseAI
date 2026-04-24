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
using TensorStack.Image;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageEditView.xaml
    /// </summary>
    public partial class ImageEditView : ViewBaseDiffusion
    {
        private ImageInput _sourceImage1;
        private ImageInput _sourceImage2;
        private ImageInput _sourceImage3;
        private ImageInput _sourceImage4;

        public ImageEditView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageEditView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        public override View View => View.ImageEdit;

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
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[ImageEdit] [Execute] Executing pipeline...");

            try
            {
                var previousImage = ResultImage;
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Images
                var inputImages = new List<ImageTensor>
                {
                    { _sourceImage1, Options.InputImageCount },
                    { _sourceImage2, Options.InputImageCount },
                    { _sourceImage3, Options.InputImageCount },
                    { _sourceImage4, Options.InputImageCount }
                };

                // Options
                var options = Options with { };
                options.InputImages = inputImages;

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = previousImage;

                // History
                await SaveHistoryAsync(options);
                Logger.LogInformation("[ImageEdit] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageEdit] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageEdit] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected override async Task ExecuteAutomationAsync()
        {
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[ImageEdit] [ExecuteAutomation] Executing pipeline...");

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
                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage1 = automationJob.InputImages[0];

                    // Images
                    var inputImages = new List<ImageTensor>
                    {
                        { _sourceImage1, Options.InputImageCount },
                        { _sourceImage2, Options.InputImageCount },
                        { _sourceImage3, Options.InputImageCount },
                        { _sourceImage4, Options.InputImageCount }
                    };

                    // Diffusion
                    var resultTensor = await ExecuteImageDiffusionAsync(automationJob.DiffusionOptions with
                    {
                        InputImages = inputImages
                    });

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = SourceImage1;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.DiffusionOptions);
                    }

                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageEdit] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageEdit] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[ImageEdit] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        private async Task<ImageInput> SaveHistoryAsync(DiffusionInputOptions options)
        {
            Logger.LogInformation($"[ImageEdit] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.ImageEdit,
            });
            Logger.LogInformation($"[ImageEdit] [SaveHistory] History saved.");
            return result;
        }
    }
}