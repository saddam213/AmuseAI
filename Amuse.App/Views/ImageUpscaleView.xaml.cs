using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Image;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageUpscaleView.xaml
    /// </summary>
    public partial class ImageUpscaleView : ViewBaseModel
    {
        private ImageInput _sourceImage;
        private ImageInput _resultImage;
        private ImageInput _compareImage;
        private UpscaleInputOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageUpscaleView"/> class.
        /// </summary>
        public ImageUpscaleView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, IUpscaleService upscaleService, ILogger<ImageUpscaleView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            UpscaleService = upscaleService;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.ImageUpscale;

        /// <summary>
        /// Gets the upscale service.
        /// </summary>
        public IUpscaleService UpscaleService { get; }

        /// <summary>
        /// Gets or sets the result image.
        /// </summary>
        public ImageInput ResultImage
        {
            get { return _resultImage; }
            set { SetProperty(ref _resultImage, value); }
        }

        /// <summary>
        /// Gets or sets the compare image.
        /// </summary>
        public ImageInput CompareImage
        {
            get { return _compareImage; }
            set { SetProperty(ref _compareImage, value); }
        }

        /// <summary>
        /// Gets or sets the source image.
        /// </summary>
        public ImageInput SourceImage
        {
            get { return _sourceImage; }
            set { SetProperty(ref _sourceImage, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public UpscaleInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }


        /// <summary>
        /// On view opened
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            ModelControl.SetPipeline(UpscaleService.Pipeline);
            IsPipelineLoaded = UpscaleService.IsLoaded && UpscaleService.Pipeline.UpscaleModel == CurrentPipeline?.UpscaleModel;
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageUpscale] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"Loading {CurrentPipeline.UpscaleModel.Name}...");

                await UpscaleService.LoadAsync(CurrentPipeline);
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[ImageUpscale] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[ImageUpscale] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ImageUpscale] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Load Pipeline", ex.Message);
                return false;
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Unload pipeline
        /// </summary>
        protected override async Task<bool> UnloadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageUpscale] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await UpscaleService.UnloadAsync();
                Logger.LogInformation("[ImageUpscale] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ImageUpscale] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Unload Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageUpscale] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Run Upscaler
                var resultTensor = await UpscaleService.ExecuteAsync(new UpscaleImageRequest
                {
                    Image = _sourceImage,
                    Options = _options
                }, ProgressCallback);

                Statistics.Stop();

                // Set Result
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = _sourceImage;

                // History
                await HistoryService.AddAsync(_resultImage, new UpscaleHistory
                {
                    Options = _options,
                    Model = CurrentPipeline.UpscaleModel.Name,
                    Source = View.ImageUpscale,
                    OriginalWidth = _sourceImage.Width,
                    OriginalHeight = _sourceImage.Height,
                    ScaleFactor = CurrentPipeline.UpscaleModel.ScaleFactor
                });

                Logger.LogInformation("[ImageUpscale] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageUpscale] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = UpscaleService.IsLoaded;
                Logger.LogError(ex, "[ImageUpscale] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[ImageUpscale] [ExecuteAutomation] Executing pipeline...");

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
                    SourceImage = automationJob.InputImages[0];

                    // Upscale
                    var resultTensor = await UpscaleService.ExecuteAsync(new UpscaleImageRequest
                    {
                        Image = _sourceImage,
                        Options = automationJob.UpscaleOptions
                    }, ProgressCallback);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = _sourceImage;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await HistoryService.AddAsync(_resultImage, new UpscaleHistory
                        {
                            Options = automationJob.UpscaleOptions,
                            Model = CurrentPipeline.UpscaleModel.Name,
                            Source = View.ImageUpscale,
                            OriginalWidth = _sourceImage.Width,
                            OriginalHeight = _sourceImage.Height,
                            ScaleFactor = CurrentPipeline.UpscaleModel.ScaleFactor
                        });
                    }

                    // Output
                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageUpscale] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageUpscale] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = UpscaleService.IsLoaded;
                Logger.LogError(ex, "[ImageUpscale] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            return _sourceImage is not null && UpscaleService.IsLoaded && !UpscaleService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            return UpscaleService.IsLoaded && !UpscaleService.IsExecuting && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            if (UpscaleService.IsLoading)
                CurrentPipeline = null;

            await UpscaleService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return UpscaleService.CanCancel;
        }


        /// <summary>
        /// Called when Upscale model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedUpscalerChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;

                if (pipeline?.UpscaleModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"Loading {pipeline.UpscaleModel.Name}...");

                    if (!await DownloadModels(pipeline))
                        return; // Canceled/Failed to download models

                    if (!await LoadPipelineAsync())
                        return; // Canceled/Failed to load pipeline

                    IsPipelineLoaded = true;
                }
            }
            finally
            {
                Progress.Clear();
                Statistics.Clear();
            }
        }
    }

}