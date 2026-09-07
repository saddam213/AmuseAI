using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using TensorStack.Common.Pipeline;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBaseLanguage"/> class.
        /// </summary>
        public ViewBaseLanguage(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger logger)
            : base(settings, navigationService, downloadService, historyService, logger)
        {
            GenerateService = generateService;
            ExtractService = extractService;
            UpscaleService = upscaleService;
            Statistics = new StatisticsModel(Dispatcher);
            ProgressCallback = new Progress<RunProgress>(OnProgress);
            ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, CanExecute);
            ExecuteAutomationCommand = new AsyncRelayCommand(ExecuteAutomationAsync, CanExecuteAutomation);
            StopCommand = new AsyncRelayCommand(GenerateService.StopAsync);
            PipelineProgressCallback = new Progress<PipelineProgress>(OnProgress);
            AutomationProgress = new ProgressInfo();
        }

        /// <summary>
        /// Gets the diffusion service.
        /// </summary>
        public IGenerateService GenerateService { get; }

        /// <summary>
        /// Gets the extract service.
        /// </summary>
        public IExtractService ExtractService { get; }

        /// <summary>
        /// Gets the upscale service.
        /// </summary>
        public IUpscaleService UpscaleService { get; }

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
            IsPipelineLoaded = GenerateService.IsLoaded && GenerateService.Pipeline == CurrentPipeline;
            Logger.LogInformation("[{View}] [Open] View opened, IsPipelineLoaded: {IsPipelineLoaded}", ViewName, IsPipelineLoaded);
            return base.OpenAsync(args);
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
                await DialogService.ShowErrorAsync("Load Pipeline", ex.Message);
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
                if (ExtractService.IsLoaded)
                {
                    await ExtractService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded extract model.", ViewName);
                }

                if (GenerateService.IsLoaded)
                {
                    await GenerateService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded language model.", ViewName);
                }

                if (UpscaleService.IsLoaded)
                {
                    await UpscaleService.UnloadAsync();
                    Logger.LogInformation("[{View}] [UnloadPipeline] Unloaded upscale model.", ViewName);
                }

                Logger.LogInformation("[{View}] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{View}] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Unload Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Determines whether this process can execute.
        /// </summary>
        protected virtual bool CanExecute()
        {
            return !GenerateService.IsExecuting
                && !UpscaleService.IsExecuting
                && !ExtractService.IsExecuting;
        }


        /// <summary>
        /// Determines whether this process can execute automations.
        /// </summary>
        protected virtual bool CanExecuteAutomation()
        {
            return CanExecute();
        }


        /// <summary>
        /// Cancels the LoadPipeline or Execute processes.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await base.CancelAsync();

            var timestamp = Stopwatch.GetTimestamp();
            if (UpscaleService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling upscale process...", ViewName);
                await UpscaleService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Upscale process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }

            if (ExtractService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling extract process...", ViewName);
                await ExtractService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Extract process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }

            if (GenerateService.CanCancel)
            {
                Logger.LogInformation("[{View}] [Cancel] Canceling generation process...", ViewName);
                await GenerateService.CancelAsync();
                Logger.LogInformation("[{View}] [Cancel] Generation process canceled, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
            }
        }


        /// <summary>
        /// Determines whether this process can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return base.CanCancel()
                || GenerateService.CanCancel
                || UpscaleService.CanCancel
                || ExtractService.CanCancel;
        }


        /// <summary>
        /// Load the Language model
        /// </summary>
        private async Task<bool> LoadLanguageModelAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (CurrentPipeline.LanguageModel is not null)
            {
                if (GenerateService.IsLoaded)
                {
                    if (GenerateService.Pipeline.IsLoadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadLanguageModel] Loading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                        await GenerateService.LoadAsync(CurrentPipeline, PipelineProgressCallback);
                    }
                    else if (GenerateService.Pipeline.IsReloadRequired(CurrentPipeline))
                    {
                        Logger.LogInformation("[{View}] [LoadLanguageModel] Reloading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                        await GenerateService.ReloadAsync(CurrentPipeline, PipelineProgressCallback);
                    }
                    else
                    {
                        await GenerateService.UpdateAsync(CurrentPipeline);
                    }
                    return true;
                }

                Logger.LogInformation("[{View}] [LoadLanguageModel] Loading language model {Name}...", ViewName, CurrentPipeline.LanguageModel.Name);
                await GenerateService.LoadAsync(CurrentPipeline, PipelineProgressCallback);
                Logger.LogInformation("[{View}] [LoadLanguageModel] Successfully loaded language model, Elapsed: {Elapsed:c}", ViewName, Stopwatch.GetElapsedTime(timestamp));
                return true;
            }

            await GenerateService.UnloadAsync();
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

            var textResult = await GenerateService.GenerateTextAsync(options);

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
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;
                Logger.LogInformation("[{View}] [SelectedPipelineChanged] A new pipeline has been created.", ViewName);
                if (pipeline?.LanguageModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"Initializing {CurrentPipeline.LanguageModel.Backend} Environment...");
                    if (!await LoadPipelineAsync())
                        return;// Canceled/Failed to load pipeline

                    IsPipelineLoaded = true;
                }
            }
            finally
            {
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
                Progress.Update(progress.Value, progress.Maximum, $"Tile {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate("Rendering Image...");

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
                    if (GenerateService.IsExecuting)
                    {
                        var message = progress.Subkey == "Transformer" && Options.Beams > 1
                            ? "Generating Beam Results..."
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
