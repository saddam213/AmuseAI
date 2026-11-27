using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Common;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsImageView.xaml
    /// </summary>
    public partial class SettingsImageView : SettingsDiffusionViewBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsImageView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="logger">The logger.</param>
        public SettingsImageView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger<SettingsImageView> logger)
            : base(MediaType.Image, settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.SettingsImage;
    }
}
