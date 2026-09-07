using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Media.Image;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageToTextView.xaml
    /// </summary>
    public partial class ImageToTextView : ViewBaseLanguage
    {
        private ImageInput _sourceImage1;
        private ImageInput _sourceImage2;
        private ImageInput _sourceImage3;
        private ImageInput _sourceImage4;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageToTextView"/> class.
        /// </summary>
        public ImageToTextView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<ImageToTextView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.ImageToText;

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
            Logger.LogInformation($"[ImageToText] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                await TextResultElement.ResetAsync();
                Statistics.Start();

                // Images
                var inputImages = GetInputTensors();
                var imageIndex = inputImages.GetIndexedInputs();

                // System Prompt
                await TextResultElement.AddSystemPromptAsync(Options.Prompt2);

                // User Prompt
                await TextResultElement.AddUserPromptAsync(Options.Prompt, imageIndex);

                // Options
                var options = Options with
                {
                    Prompt = null,
                    Prompt2 = null,
                    InputImages = inputImages.AsImageTensors(),
                    Conversation = TextResultElement.Conversation
                };

                // Execute
                var textResult = await ExecuteLanguageModelAsync(options);

                // Result
                await TextResultElement.EndStreamResponseAsync();
                TextResultElement.AddBeamResults(textResult.Results);
                Statistics.Stop();

                // History
                await SaveHistoryAsync(textResult.Result, options);
                Logger.LogInformation("[ImageToText] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToText] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToText] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
                await TextResultElement.EndStreamResponseAsync();
            }
        }


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        protected override async Task ExecuteAutomationAsync()
        {
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[ImageToText] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate($"Automation Started");
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Text, MediaType.Image))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await TextResultElement.ResetAsync();

                    // Source
                    if (!automationJob.InputImages.IsNullOrEmpty())
                        SourceImage1 = automationJob.InputImages[0];

                    // Images
                    var inputImages = GetInputTensors();
                    var imageIndex = inputImages.GetIndexedInputs();

                    // System Prompt
                    await TextResultElement.AddSystemPromptAsync(Options.Prompt2);

                    // User Prompt
                    await TextResultElement.AddUserPromptAsync(Options.Prompt, imageIndex);

                    // Options
                    var options = automationJob.GenerateOptions with
                    {
                        Prompt = null,
                        Prompt2 = null,
                        InputImages = inputImages.AsImageTensors(),
                        Conversation = TextResultElement.Conversation
                    };

                    // Execute
                    var textResult = await ExecuteLanguageModelAsync(options);

                    // Result
                    await TextResultElement.EndStreamResponseAsync();
                    TextResultElement.AddBeamResults(textResult.Results);
                    Statistics.Stop();

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(textResult.Result, options);
                    }

                    await automationJob.SaveAsync(Utils.GetResponseText(textResult.Result.Text));
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[ImageToText] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[ImageToText] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[ImageToText] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Automation", ex.Message);
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
        /// Determines whether this process can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return base.CanExecute()
                && _sourceImage1 != null
                && !string.IsNullOrEmpty(Options?.Prompt);
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            return base.CanExecute()
                && !string.IsNullOrEmpty(Options?.Prompt);
        }


        /// <summary>
        /// Gets the input tensors.
        /// </summary>
        /// <returns>List&lt;ImageTensor&gt;.</returns>
        private List<ImageInput> GetInputTensors()
        {
            var inputImages = new List<ImageInput>();
            if (Options.IsSource1Enabled)
                inputImages.AddIfNotNull(_sourceImage1);
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
        private async Task<TextInput> SaveHistoryAsync(TextInput textResult, GenerateInputOptions options)
        {
            Logger.LogInformation($"[ImageToText] [SaveHistory] Saving history...");
            textResult.Text = Utils.GetResponseText(textResult.Text);
            var result = await HistoryService.AddAsync(textResult, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.LanguageModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.ImageToText,
            });
            Logger.LogInformation($"[ImageToText] [SaveHistory] History saved.");
            return result;
        }


        /// <summary>
        /// Called when progress is received from a pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected override void OnProgress(PipelineProgress progress)
        {
            base.OnProgress(progress);
            if (progress.Key == "Generate" && progress.Subkey == "Token")
            {
                TextResultElement.UpdateStreamResponse(progress.Message, progress.Value);
            }
        }

    }
}
