using Amuse.App.Common;
using Amuse.App.Controls;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    public abstract class ViewBase : ViewControl
    {
        private bool _isViewBusy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewBase"/> class.
        /// </summary>
        public ViewBase(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IHistoryService historyService, ILogger logger)
            : base(navigationService)
        {
            Logger = logger;
            Settings = settings;
            HistoryService = historyService;
            DownloadService = downloadService;
            Progress = new ProgressInfo();
            ViewName = View.ToString();
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public abstract View View { get; }

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        public override int Id => (int)View;

        /// <summary>
        /// Gets the name of the view.
        /// </summary>
        public string ViewName { get; }

        /// <summary>
        /// Gets the logger.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        public Settings Settings { get; }

        /// <summary>
        /// Gets the progress.
        /// </summary>
        public ProgressInfo Progress { get; }

        /// <summary>
        /// Gets the history service.
        /// </summary>
        public IHistoryService HistoryService { get; }

        /// <summary>
        /// Gets the download service.
        /// </summary>
        public IModelDownloadService DownloadService { get; }

        /// <summary>
        /// Gets or sets the cancel command.
        /// </summary>
        public AsyncRelayCommand CancelCommand { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token source.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this view busy.
        /// </summary>
        /// <value><c>true</c> if this i view busy; otherwise, <c>false</c>.</value>
        public bool IsViewBusy
        {
            get { return _isViewBusy; }
            set { SetProperty(ref _isViewBusy, value); }
        }


        /// <summary>
        /// On View Open
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            // Select after the derived view has restored its pipeline
            if (args is ModelOpenArgs modelOpenArgs)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => SelectModel(modelOpenArgs.Model));

            return base.OpenAsync(args);
        }


        /// <summary>
        /// Selects the model in the view's model control, if the view has one.
        /// </summary>
        /// <param name="model">The model.</param>
        private void SelectModel(IGenerateModel model)
        {
            var modelControl = FindName("ModelControl");
            if (modelControl is DiffusionModelControl diffusionModelControl && model is DiffusionModel diffusionModel)
                diffusionModelControl.SelectModel(diffusionModel);
            else if (modelControl is LanguageModelControl languageModelControl && model is LanguageModel languageModel)
                languageModelControl.SelectModel(languageModel);
        }


        /// <summary>
        /// Cancel as an asynchronous operation.
        /// </summary>
        protected virtual async Task CancelAsync()
        {
            if (CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
                await CancellationTokenSource.CancelAsync();
        }


        /// <summary>
        /// Determines whether this process can cancel.
        /// </summary>
        protected virtual bool CanCancel()
        {
            return CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested;
        }
    }
}
