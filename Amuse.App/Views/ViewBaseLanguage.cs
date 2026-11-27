using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using TensorStack.Common.Pipeline;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;
using PipelineProgress = Amuse.Common.PipelineProgress;

namespace Amuse.App.Views
{
    public abstract class ViewBaseLanguage : ViewBase
    {
        private bool _isPipelineLoaded;
        private PipelineModel _currentPipeline;
        private TextResult _resultText;
        private GenerateInputOptions _options;
        private AutomationOptions _automationOptions;
        private bool _isAutomating;
        private bool _isPipelineBusy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBaseLanguage"/> class.
        /// </summary>
        public ViewBaseLanguage(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, ILanguageService languageService, IGenerateService generateService, IHistoryService historyService, IToolService toolService, ILogger logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            LanguageService = languageService;
            GenerateService = generateService;
            ToolService = toolService;
            Statistics = new StatisticsModel(Dispatcher);
            ProgressCallback = new Progress<RunProgress>(OnProgress);
            ExecuteCommand = new AsyncRelayCommand(() => ExecutePipelineAsync(ExecuteAsync), CanExecute);
            ExecuteAutomationCommand = new AsyncRelayCommand(() => ExecutePipelineAsync(ExecuteAutomationAsync), CanExecuteAutomation);
            StopCommand = new AsyncRelayCommand(LanguageService.StopAsync);
            PipelineProgressCallback = new Progress<PipelineProgress>(OnProgress);
            AutomationProgress = new ProgressInfo();
        }

        /// <summary>
        /// Gets the language service.
        /// </summary>
        public ILanguageService LanguageService { get; }

        /// <summary>
        /// Gets the generate service, its diffusion model can stay loaded next to the language model.
        /// </summary>
        public IGenerateService GenerateService { get; }

        /// <summary>
        /// Gets the tool service.
        /// </summary>
        public IToolService ToolService { get; }

        /// <summary>
        /// Gets the statistics.
        /// </summary>
        public StatisticsModel Statistics { get; }

        /// <summary>
        /// Gets or sets the execute command.
        /// </summary>
        public AsyncRelayCommand ExecuteCommand { get; set; }

        /// <summary>
        /// Gets or sets the execute automation command.
        public AsyncRelayCommand ExecuteAutomationCommand { get; set; }

        /// <summary>
        /// Gets or sets the stop command.
        /// </summary>
        public AsyncRelayCommand StopCommand { get; set; }

        /// <summary>
        /// Gets the progress callback.
        /// </summary>
        public IProgress<RunProgress> ProgressCallback { get; }

        /// <summary>
        /// Gets the pipeline progress callback.
        /// </summary>
        protected IProgress<PipelineProgress> PipelineProgressCallback { get; }

        /// <summary>
        /// Gets or sets the automation progress.
        /// </summary>
        public ProgressInfo AutomationProgress { get; }


        /// <summary>
        /// Gets or sets a value indicating whether this instance is pipeline loaded.
        /// </summary>
        public bool IsPipelineLoaded
        {
            get { return _isPipelineLoaded; }
            set { SetProperty(ref _isPipelineLoaded, value); }
        }

        /// <summary>
        /// Gets or sets the current pipeline.
        /// </summary>
        public PipelineModel CurrentPipeline
        {
            get { return _currentPipeline; }
            set { SetProperty(ref _currentPipeline, value); }
        }

        /// <summary>
        /// Gets or sets the result text.
        /// </summary>
        public TextResult ResultText
        {
            get { return _resultText; }
            set { SetProperty(ref _resultText, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public GenerateInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }

        /// <summary>
        /// Gets or sets the automation options.
        /// </summary>
        public AutomationOptions AutomationOptions
        {
            get { return _automationOptions; }
            set { SetProperty(ref _automationOptions, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is automating.
        /// </summary>
        public bool IsAutomating
        {
            get { return _isAutomating; }
            set { SetProperty(ref _isAutomating, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the view is loading, executing or automating, the model controls are locked while set.
        /// </summary>
        public bool IsPipelineBusy
        {
            get { return _isPipelineBusy; }
            set { SetProperty(ref _isPipelineBusy, value); }
        }


        /// <summary>
        /// Executes the operation with the model controls locked for every stage, not only while a service is executing.
        /// </summary>
        /// <param name="operation">The operation.</param>
        private async Task ExecutePipelineAsync(Func<Task> operation)
        {
            try
            {
                IsPipelineBusy = true;
                await operation();
            }
            finally
            {
                IsPipelineBusy = false;
            }
        }


        /// <summary>
        /// Executes the pipeline.
        /// </summary>
        protected abstract Task ExecuteAsync();


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        /// <returns>Task.</returns>
        protected abstract Task ExecuteAutomationAsync();


        /// <summary>
        ///  On View Open
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            IsPipelineLoaded = LanguageService.IsLoaded && LanguageService.Pipeline == CurrentPipeline;
            Logger.LogInformation("[{View}] [Open] View opened, IsPipelineLoaded: {IsPipelineLoaded}", ViewName, IsPipelineLoaded);
            _ = ConnectToolsAsync();
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Connects the MCP servers in the background, the view does not wait for them and a server that fails is skipped.
        /// </summary>
        private async Task ConnectToolsAsync()
        {
            try
            {
                await ToolService.ConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [ConnectTools] An exception occurred connecting the MCP servers", ViewName);
            }
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected virtual async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [LoadPipeline] Loading pipeline...", ViewName);

            try
            {
                await RequestModelAccessAsync(CurrentPipeline);
                await LoadLanguageModelAsync();
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[{View}] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[{View}] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.LoadPipelineTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Unload pipeline
        /// </summary>
        protected virtual async Task<bool> UnloadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [UnloadPipeline] Unloading pipeline...", ViewName);

            try
            {
                if (LanguageService.IsLoaded)
                {
                    await LanguageService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded language model.", ViewName);
                }

                Logger.LogInformation("[{View}] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.UnloadPipelineTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Determines whether this process can execute.
        /// </summary>
        protected virtual bool CanExecute()
        {
            return !LanguageService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected virtual bool CanExecuteAutomation()
        {
            return CanExecute() && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Cancels the LoadPipeline or Execute processes.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await base.CancelAsync();

            var timestamp = Stopwatch.GetTimestamp();
            if (LanguageService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling generation process...", ViewName);
                await LanguageService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Generation process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }
        }


        /// <summary>
        /// Determines whether this process can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return base.CanCancel() || LanguageService.CanCancel;
        }


        /// <summary>
        /// Load the Language model
        /// </summary>
        private async Task<bool> LoadLanguageModelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (CurrentPipeline.LanguageModel is not null)
            {
                if (LanguageService.IsLoaded)
                {
                    if (LanguageService.Pipeline.IsLoadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadLanguageModel] Loading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                        await LanguageService.LoadAsync(CurrentPipeline, PipelineProgressCallback);
                    }
                    else if (LanguageService.Pipeline.IsReloadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadLanguageModel] Reloading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                        await LanguageService.ReloadAsync(CurrentPipeline, PipelineProgressCallback);
                    }
                    else
                    {
                        await LanguageService.UpdateAsync(CurrentPipeline);
                    }
                    return true;
                }

                Logger.LogInformation("[{View}] [LoadLanguageModel] Loading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                await LanguageService.LoadAsync(CurrentPipeline, PipelineProgressCallback);
                Logger.LogInformation("[{View}] [LoadLanguageModel] Successfully loaded language model, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }

            await LanguageService.UnloadAsync();
            Logger.LogInformation("[{View}] [LoadLanguageModel] Unloaded language model.", ViewName);
            return false;
        }


        /// <summary>
        /// Execute text inference
        /// </summary>
        /// <param name="options">The options.</param>
        protected async Task<TextResult> ExecuteLanguageModelAsync(GenerateInputOptions options)
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[{View}] [ExecuteLanguageModel] Executing language model...", ViewName);

            var textResult = await LanguageService.GenerateTextAsync(options);

            Logger.LogInformation("[{View}] [ExecuteLanguageModel] Execution complete, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            return textResult;
        }


        /// <summary>
        /// Called when the selected Pipeline changes
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedPipelineChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineBusy = true;
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;
                Logger.LogInformation("[{View}] [SelectedPipelineChanged] A new pipeline has been created.", ViewName);
                if (pipeline?.LanguageModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate(string.Format(AppDefault.InitializingBackendEnvironment, CurrentPipeline.LanguageModel.Backend));
                    if (!await LoadPipelineAsync())
                        return;// Canceled/Failed to load pipeline

                    IsPipelineLoaded = true;
                }
            }
            finally
            {
                IsPipelineBusy = false;
                Progress.Clear();
                Statistics.Clear();
            }
        }


        /// <summary>
        /// Called when progress is received from a C# pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected virtual void OnProgress(RunProgress progress)
        {
            if (progress.Maximum > 1)
                Progress.Update(progress.Value, progress.Maximum, $"{AppDefault.Tile} {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate($"{AppDefault.RenderingImage}...");

            Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Max}, Elapsed: {Elapsed:c}", ViewName, progress.Value, progress.Maximum, progress.Elapsed);
        }


        /// <summary>
        /// Called when progress is received from a pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected virtual void OnProgress(PipelineProgress progress)
        {
            if (CurrentPipeline is null)
                return;

            if (progress.Key == "Load")
            {
                Progress.Indeterminate(progress.Message);
            }
            else if (progress.Key == "Generate")
            {
                if (progress.Subkey == "Initialize")
                {
                    CommandManager.InvalidateRequerySuggested();
                }
                else if (progress.Subkey == "Token")
                {
                    Statistics.UpdateToken(progress);
                }
                else
                {
                    if (LanguageService.IsExecuting)
                    {
                        var message = progress.Subkey == "Transformer" && Options.Beams > 1
                            ? AppDefault.GeneratingBeamResults
                            : Globalization.GetProgressMessage(progress);
                        Progress.Indeterminate(message);
                    }
                }
            }

            if (progress.Subkey != "Token")
            {
                Logger.LogDebug("[{View}] [OnProgress] {Subkey} - {Message}", ViewName, progress.Subkey, progress.Message);
            }
        }


        /// <summary>
        /// Handles the <see cref="E:MediaImport" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="MediaImportEventArgs"/> instance containing the event data.</param>
        protected async void OnMediaImport(object sender, MediaImportEventArgs args)
        {
            if (IsAutomating)
                return;

            await HistoryService.AddAsync(args);
        }


        /// <summary>
        /// Handles the <see cref="E:SourceTextKeyDown" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
        protected void OnSourceTextKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                if (CanExecute())
                    ExecuteCommand.Execute(null);
            }
        }
    }
}
