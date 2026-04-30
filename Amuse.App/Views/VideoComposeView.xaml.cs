using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Windows.Media.Imaging;
using TensorStack.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoComposeView.xaml
    /// </summary>
    public partial class VideoComposeView : ViewBase
    {

        public VideoComposeView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsControlNetView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            InitializeComponent();
        }

        public override View View => View.VideoCompose;


  

    }
}