using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    public abstract class ViewBaseModel : ViewBase
    {
        private bool _isPipelineLoaded;
        private PipelineModel _currentPipeline;
        private AutomationOptions _automationOptions;
        private bool _isAutomating;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBaseModel"/> class.
        /// </summary>
        public ViewBaseModel(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, ILogger logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            Statistics = new StatisticsModel(Dispatcher);
            ProgressCallback = new Progress<RunProgress>(OnProgress);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
            ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, CanExecute);
            ExecuteAutomationCommand = new AsyncRelayCommand(ExecuteAutomationAsync, CanExecuteAutomation);
            AutomationProgress = new ProgressInfo();
        }

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
        /// Gets or sets the cancel command.
        /// </summary>
        public AsyncRelayCommand CancelCommand { get; set; }

        /// <summary>
        /// Gets the progress callback.
        /// </summary>
        public IProgress<RunProgress> ProgressCallback { get; }

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
        /// Executes the asynchronous.
        /// </summary>
        protected abstract Task ExecuteAsync();


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        /// <returns>Task.</returns>
        protected abstract Task ExecuteAutomationAsync();


        /// <summary>
        /// Loads the pipeline asynchronous.
        /// </summary>
        protected abstract Task<bool> LoadPipelineAsync();


        /// <summary>
        /// Unloads the pipeline asynchronous.
        /// </summary>
        protected abstract Task<bool> UnloadPipelineAsync();


        /// <summary>
        /// Determines whether process can execute.
        /// </summary>
        protected virtual bool CanExecute()
        {
            return true;
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
        protected virtual Task CancelAsync()
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the process can cancel.
        /// </summary>
        protected virtual bool CanCancel()
        {
            return true;
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
    }
}
