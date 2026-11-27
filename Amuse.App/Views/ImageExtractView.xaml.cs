using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Resources;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageExtractView.xaml
    /// </summary>
    public partial class ImageExtractView : ViewBaseModel
    {
        private ImageInput _sourceImage;
        private ImageInput _resultImage;
        private ImageInput _compareImage;
        private ExtractInputOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageExtractView"/> class.
        /// </summary>
        public ImageExtractView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IExtractService extractService, ILogger<ImageExtractView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            ExtractService = extractService;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.ImageExtract;

        /// <summary>
        /// Gets the extract service.
        /// </summary>
        public IExtractService ExtractService { get; }

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
        public ExtractInputOptions Options
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
            ModelControl.SetPipeline(IsModelOwner() ? ExtractService.Pipeline : null);
            IsPipelineLoaded = IsModelOwner() && ExtractService.IsLoaded && ExtractService.Pipeline.ExtractModel == CurrentPipeline?.ExtractModel;
            return base.OpenAsync(args);
        }

        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageExtract] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"{Default.Loading} {CurrentPipeline.ExtractModel.Name}...");

                await RequestModelAccessAsync(CurrentPipeline);
                await ExtractService.LoadAsync(CurrentPipeline);
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[ImageExtract] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[ImageExtract] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ImageExtract] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.LoadPipelineTitle, ex.Message);
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
            Logger.LogInformation("[ImageExtract] [UnloadPipeline] Unloading pipeline...");

            try
            {
                if (IsModelOwner())
                    await ExtractService.UnloadAsync();
                Logger.LogInformation("[ImageExtract] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ImageExtract] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.UnloadPipelineTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Determines whether this view loaded the extract model, rather than a diffusion pipeline.
        /// </summary>
        private bool IsModelOwner()
        {
            return ModelAccessService.GetExtractOwner() == ModelOwnerType.Extract;
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[ImageExtract] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();

                // Run Extractor
                var resultTensor = await ExtractService.ExecuteAsync(new ExtractImageRequest
                {
                    Image = _sourceImage,
                    Options = _options,
                }, ProgressCallback);

                Statistics.Stop();

                // Set Result
                ResultImage = await resultTensor.ToImageInputAsync();
                CompareImage = _sourceImage;

                // History
                await HistoryService.AddAsync(_resultImage, new ExtractHistory
                {
                    Options = _options,
                    Model = CurrentPipeline.ExtractModel.Name,
                    ExtractorType = CurrentPipeline.ExtractModel.Type,
                    Source = View.ImageExtract,
                });

                Logger.LogInformation("[ImageExtract] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageExtract] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = ExtractService.IsLoaded;
                Logger.LogError(ex, "[ImageExtract] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[ImageExtract] [ExecuteAutomation] Executing pipeline...");

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
                    cancellationToken.ThrowIfCancellationRequested();

                    // Source
                    SourceImage = automationJob.InputImages[0];

                    // Extract
                    var resultTensor = await ExtractService.ExecuteAsync(new ExtractImageRequest
                    {
                        Image = _sourceImage,
                        Options = automationJob.ExtractOptions,
                    }, ProgressCallback);

                    // Result
                    ResultImage = await resultTensor.ToImageInputAsync();
                    CompareImage = _sourceImage;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await HistoryService.AddAsync(_resultImage, new ExtractHistory
                        {
                            Options = automationJob.ExtractOptions,
                            Model = CurrentPipeline.ExtractModel.Name,
                            ExtractorType = CurrentPipeline.ExtractModel.Type,
                            Source = View.ImageExtract,
                        });
                    }

                    // Output
                    await automationJob.SaveAsync(ResultImage);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageExtract] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageExtract] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = ExtractService.IsLoaded;
                Logger.LogError(ex, "[ImageExtract] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Determines whether this instance can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return _sourceImage is not null && ExtractService.IsLoaded && IsModelOwner() && !ExtractService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            return ExtractService.IsLoaded && IsModelOwner() && !ExtractService.IsExecuting && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await base.CancelAsync();
            await ExtractService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return base.CanCancel() || ExtractService.CanCancel;
        }


        /// <summary>
        /// Called when Extract model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedExtractorChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;

                if (pipeline?.ExtractModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"{Default.Loading} {pipeline.ExtractModel.Name}...");

                    if (!await LoadPipelineAsync())
                        return;   // Canceled/Failed to load pipeline

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