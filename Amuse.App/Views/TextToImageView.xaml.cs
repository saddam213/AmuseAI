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
    /// Interaction logic for TextToImageView.xaml
    /// </summary>
    public partial class TextToImageView : ViewBaseDiffusion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextToImageView"/> class.
        /// </summary>
        public TextToImageView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<TextToImageView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextToImage;


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
            Logger.LogInformation($"[TextToImage] [Execute] Executing pipeline...");

            try
            {
                var previousImage = ResultImage;
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Diffusion
                var options = Options with { };
                var resultTensor = await ExecuteImageDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = previousImage;

                // History
                await SaveHistoryAsync(options);
                Logger.LogInformation("[TextToImage] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToImage] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[TextToImage] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[TextToImage] [ExecuteAutomation] Executing pipeline...");

            try
            {
                var previousImage = ResultImage;
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Loading Automations...");
                var automationJobs = await AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Image, MediaType.Text);
                AutomationProgress.Update(0, automationJobs.Count, $"Automation: {0}/{automationJobs.Count}");
                foreach (var automationJob in automationJobs)
                {
                    // Diffusion
                    var resultTensor = await ExecuteImageDiffusionAsync(automationJob.DiffusionOptions);

                    // Upscale
                    resultTensor = await ExecuteImageUpscaleAsync(resultTensor);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.DiffusionOptions);
                    }

                    // Output
                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJobs.Count, $"Automation: {automationJob.Id}/{automationJobs.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[TextToImage] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToImage] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[TextToImage] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[TextToImage] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultImage, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.TextToImage,
            });
            Logger.LogInformation($"[TextToImage] [SaveHistory] History saved.");
            return result;
        }
    }
}