using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Image;
using TensorStack.Video;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoComposeView.xaml
    /// </summary>
    public partial class VideoComposeView : ViewBase
    {
        public VideoComposeView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, IMediaService mediaService, ILogger<SettingsControlNetView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            MediaService = mediaService;
            InitializeComponent();
        }

        public override View View => View.VideoCompose;
        public IMediaService MediaService { get; }


        protected async void OnImageCreated(object sender, ImageInput e)
        {
            await HistoryService.AddAsync(e, new ComposeHistory { Model = "None", Source = View, });
        }


        protected async void OnVideoCreated(object sender, VideoInputStream e)
        {
            await HistoryService.AddAsync(e, new ComposeHistory { Model = "None", Source = View, });
        }
    }
}