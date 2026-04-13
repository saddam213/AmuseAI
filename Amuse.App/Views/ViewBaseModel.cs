using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    public abstract class ViewBaseModel : ViewBase
    {
        private bool _isPipelineLoaded;
        private PipelineModel _currentPipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBaseModel"/> class.
        /// </summary>
        public ViewBaseModel(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            Statistics = new StatisticsModel(Dispatcher);
            ProgressCallback = new Progress<RunProgress>(OnProgress);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
            ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, CanExecute);
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
        /// Gets or sets the cancel command.
        /// </summary>
        public AsyncRelayCommand CancelCommand { get; set; }

        /// <summary>
        /// Gets the progress callback.
        /// </summary>
        public IProgress<RunProgress> ProgressCallback { get; }

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
        /// Executes the asynchronous.
        /// </summary>
        protected abstract Task ExecuteAsync();


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
