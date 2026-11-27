using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for AudioComposeView.xaml
    /// </summary>
    public partial class AudioComposeView : ViewBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioComposeView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public AudioComposeView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IMediaService mediaService, ILogger<AudioComposeView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            MediaService = mediaService;
            InitializeComponent();
        }

        public override View View => View.AudioCompose;
        public IMediaService MediaService { get; }


        /// <summary>
        /// Saves the composed audio to history.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The composed audio.</param>
        protected async void OnAudioCreated(object sender, AudioInputStream e)
        {
            await HistoryService.AddAsync(e, new AudioHistory { Model = "None", Source = View, });
        }


        /// <summary>
        /// Saves the reference video with the composed audio to history.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The video.</param>
        protected async void OnVideoCreated(object sender, VideoInputStream e)
        {
            await HistoryService.AddAsync(e, new ComposeHistory { Model = "None", Source = View, });
        }
    }
}
