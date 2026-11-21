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
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
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

        public ImageEditView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageEditView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
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
                ModelControl.SetPipeline(GenerateService.Pipeline);
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

                // Options
                var options = Options with { InputImages = GetInputTensors() };

                // Execute
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = _sourceImage1;

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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageEdit] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Image))
                {
                    PreviewImage = default;
                    cancellationToken.ThrowIfCancellationRequested();

                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage1 = automationJob.InputImages[0];

                    // Options
                    var options = automationJob.GenerateOptions with { InputImages = GetInputTensors() };

                    // Execute
                    var resultTensor = await ExecuteImageDiffusionAsync(options);

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = _sourceImage1;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.GenerateOptions);
                    }

                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageEdit] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Gets the input tensors.
        /// </summary>
        private List<ImageTensor> GetInputTensors()
        {
            var inputImages = new List<ImageTensor>();
            var inputImage = ImageEditControl.GetImageCanvas();
            if (Options.IsSource1Enabled)
                inputImages.AddIfNotNull(inputImage);
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
        private async Task<ImageInput> SaveHistoryAsync(GenerateInputOptions options)
        {
            Logger.LogInformation($"[ImageEdit] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = UpscaledModel?.Name,
                UpscaleOptions = UpscaledModel is not null ? UpscaleOptions : null,
                Source = View.ImageEdit,
            });
            Logger.LogInformation($"[ImageEdit] [SaveHistory] History saved.");
            return result;
        }
    }
}