using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Common;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsVideoView.xaml
    /// </summary>
    public partial class SettingsVideoView : SettingsDiffusionViewBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsVideoView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="logger">The logger.</param>
        public SettingsVideoView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger<SettingsVideoView> logger)
            : base(MediaType.Video, settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.SettingsVideo;
    }
}
