using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Windows.Media.Imaging;
using TensorStack.Common;
using TensorStack.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ImageComposeView.xaml
    /// </summary>
    public partial class ImageComposeView : ViewBase
    {

        public ImageComposeView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsControlNetView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            InitializeComponent();
        }

        public override View View => View.ImageCompose;


        protected async void LayerControl_ImageGenerated(object sender, BitmapSource image)
        {
            var inputImage = new ImageInput(image);
            await HistoryService.AddAsync(inputImage, new ImageLayerHistory
            {
                Source = View.ImageCompose,
                MediaType = MediaType.Image,
                Model = "Layer",
                Width = inputImage.Width,
                Height = inputImage.Height,
                Timestamp = DateTime.UtcNow
            });
        }

    }
}