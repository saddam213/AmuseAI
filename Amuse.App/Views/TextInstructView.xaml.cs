using Amuse.App.Common;
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
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for TextInstructView.xaml
    /// </summary>
    public partial class TextInstructView : ViewBaseLanguage
    {
        private string _automationPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextInstructView"/> class.
        /// </summary>
        public TextInstructView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<TextInstructView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextInstruct;

        /// <summary>
        /// Gets or sets the automation prompt.
        /// </summary>
        public string AutomationPrompt
        {
            get { return _automationPrompt; }
            set { SetProperty(ref _automationPrompt, value); }
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
            Logger.LogInformation($"[TextInstruct] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                await TextResultElement.ResetAsync();
                Statistics.Start();

                // Context
                var prompt = Options.Prompt;
                var systemPrompt = Options.Prompt2;
                var promptInputs = InputControl.GetPromptInputs(prompt);

                // Conversation
                await TextResultElement.AddSystemPromptAsync(systemPrompt);

                // User Prompt
                await TextResultElement.AddUserPromptAsync(promptInputs.Prompt, promptInputs.ImageIndex, promptInputs.AudioIndex, promptInputs.VideoIndex);

                // Options
                var options = Options with
                {
                    Prompt = null,
                    Prompt2 = null,
                    InputAudios = promptInputs.AudioContext,
                    InputImages = promptInputs.ImageContext.AsImageTensors(),
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
                Logger.LogInformation("[TextInstruct] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextInstruct] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[TextInstruct] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[TextInstruct] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();
                await TextResultElement.ResetAsync();

                AutomationProgress.Indeterminate($"Automation Started");
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Text, MediaType.Text))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await TextResultElement.ResetAsync();
                    AutomationPrompt = $"{Options.Prompt}{automationJob.GenerateOptions.Prompt}";

                    // Context
                    var prompt = AutomationPrompt;
                    var systemPrompt = Options.Prompt2;
                    var promptInputs = InputControl.GetPromptInputs(prompt);

                    // Conversation
                    await TextResultElement.AddSystemPromptAsync(systemPrompt);

                    // User Prompt
                    await TextResultElement.AddUserPromptAsync(promptInputs.Prompt, promptInputs.ImageIndex, promptInputs.AudioIndex, promptInputs.VideoIndex);

                    // Options
                    var options = automationJob.GenerateOptions with
                    {
                        Prompt = null,
                        Prompt2 = null,
                        InputAudios = promptInputs.AudioContext,
                        InputImages = promptInputs.ImageContext.AsImageTensors(),
                        Conversation = TextResultElement.Conversation
                    };

                    // Execute
                    var textResult = await ExecuteLanguageModelAsync(options);

                    // Result
                    await TextResultElement.EndStreamResponseAsync();
                    TextResultElement.AddBeamResults(textResult.Results);

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(textResult.Result, options);
                    }

                    await automationJob.SaveAsync(Utils.GetResponseText(textResult.Result.Text));
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[TextInstruct] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextInstruct] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[TextInstruct] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Automation", ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = null;
                AutomationPrompt = null;
            }
        }


        /// <summary>
        /// Determines whether this process can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return base.CanExecute() && !string.IsNullOrEmpty(Options?.Prompt);
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<TextInput> SaveHistoryAsync(TextInput textResult, GenerateInputOptions options)
        {
            Logger.LogInformation($"[TextInstruct] [SaveHistory] Saving history...");
            textResult.Text = Utils.GetResponseText(textResult.Text);
            var result = await HistoryService.AddAsync(textResult, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.LanguageModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.TextInstruct,
            });
            Logger.LogInformation($"[TextInstruct] [SaveHistory] History saved.");
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
