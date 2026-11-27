using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Media.Video;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoComposeView.xaml
    /// </summary>
    public partial class VideoComposeView : ViewBase
    {
        public VideoComposeView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IMediaService mediaService, ILogger<SettingsControlNetView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
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