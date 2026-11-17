using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Common;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsAudioView.xaml
    /// </summary>
    public partial class SettingsAudioView : SettingsDiffusionViewBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsAudioView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="logger">The logger.</param>
        public SettingsAudioView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsAudioView> logger)
            : base(MediaType.Audio, settings, navigationService, downloadService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.SettingsAudio;
    }
}
