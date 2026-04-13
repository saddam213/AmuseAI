using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
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
        public ViewBase(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger logger)
            : base(navigationService)
        {
            Logger = logger;
            Settings = settings;
            EnvironmentService = environmentService;
            HistoryService = historyService;
            DownloadService = downloadService;
            Progress = new ProgressInfo();
            ViewName = View.ToString();
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
        /// Gets the environment service.
        /// </summary>
        public IEnvironmentService EnvironmentService { get; }

        /// <summary>
        /// Gets the download service.
        /// </summary>
        public IDownloadService DownloadService { get; }

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
        /// Downloads the models.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <returns><c>true</c> if download succeeded, <c>false</c> otherwise.</returns>
        protected async Task<bool> DownloadModels(PipelineModel pipeline)
        {
            if (pipeline.UpscaleModel is not null && pipeline.UpscaleModel.Status != ModelStatusType.Installed)
            {
                Logger.LogInformation("[{View}] [DownloadModels] Download upscale model '{Name}'...", View, pipeline.UpscaleModel.Name);
                if (!await pipeline.UpscaleModel.DownloadAsync(Path.Combine(Settings.DirectoryModel, "Upscale")))
                {
                    Logger.LogError("[{View}] [DownloadModels] Failed to download upscale model...", View);
                    return false;
                }
                Logger.LogInformation("[{View}] [DownloadModels] Successfully downloaded upscale model.", View);
            }

            if (pipeline.ExtractModel is not null && pipeline.ExtractModel.Status != ModelStatusType.Installed)
            {
                Logger.LogInformation("[{View}] [DownloadModels] Download extract model '{Name}'...", View, pipeline.ExtractModel.Name);
                if (!await pipeline.ExtractModel.DownloadAsync(Path.Combine(Settings.DirectoryModel, "Extract")))
                {
                    Logger.LogError("[{View}] [DownloadModels] Failed to download extract model...", View);
                    return false;
                }
                Logger.LogInformation("[{View}] [DownloadModels] Successfully downloaded extract model.", View);
            }

            if (pipeline.AudioModel is not null && pipeline.AudioModel.Status != ModelStatusType.Installed)
            {
                Logger.LogInformation("[{View}] [DownloadModels] Download audio model '{Name}'...", View, pipeline.AudioModel.Name);
                if (!await pipeline.AudioModel.DownloadAsync(Path.Combine(Settings.DirectoryModel, "Audio")))
                {
                    Logger.LogError("[{View}] [DownloadModels] Failed to download audio model...", View);
                    return false;
                }
                Logger.LogInformation("[{View}] [DownloadModels] Successfully downloaded audio model.", View);
            }

            Settings.ScanModels();
            return true;
        }
    }
}
