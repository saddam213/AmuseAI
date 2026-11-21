using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for AppUpdateDialog.xaml
    /// </summary>
    public partial class AppUpdateDialog : DialogControl
    {
        private readonly Settings _settings;
        private readonly DownloadService _downloadService;
        private readonly IProgress<FileDownloadProgress> _updateProgress;
        private AppUpdate _appUpdate;
        private AppAsset _appAsset;
        private CancellationTokenSource _cancellationTokenSource;
        private string _assetLocation;
        private string _errorMessage;
        private double _downloadProgress;
        private double _downloadSpeed;
        private double _downloadSize;
        private double _downloadAmount;
        private string _downloadRemaining = "0 sec";
        private DateTime _lastUpdate;

        public AppUpdateDialog(Settings settings, DownloadService downloadService)
        {
            _settings = settings;
            _downloadService = downloadService;
            _updateProgress = new Progress<FileDownloadProgress>(UpdateProgress);
            DownloadCommand = new AsyncRelayCommand(Download);
            InitializeComponent();
        }

        public AsyncRelayCommand DownloadCommand { get; }
        public string AssetLocation => _assetLocation;

        public AppUpdate AppUpdate
        {
            get { return _appUpdate; }
            set { SetProperty(ref _appUpdate, value); }
        }

        public AppAsset AppAsset
        {
            get { return _appAsset; }
            set { SetProperty(ref _appAsset, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public double DownloadProgress
        {
            get { return _downloadProgress; }
            set { SetProperty(ref _downloadProgress, value); }
        }

        public double DownloadSpeed
        {
            get { return _downloadSpeed; }
            set { SetProperty(ref _downloadSpeed, value); }
        }

        public double DownloadSize
        {
            get { return _downloadSize; }
            set { SetProperty(ref _downloadSize, value); }
        }

        public double DownloadAmount
        {
            get { return _downloadAmount; }
            set { SetProperty(ref _downloadAmount, value); }
        }

        public string DownloadRemaining
        {
            get { return _downloadRemaining; }
            set { SetProperty(ref _downloadRemaining, value); }
        }


        public async Task<bool> ShowDialogAsync(AppUpdate appUpdate, bool isAppInstalled)
        {
            AppUpdate = appUpdate;
            AppAsset = isAppInstalled
                ? appUpdate.AssetInstaller
                : appUpdate.AssetStandalone;
            DownloadSize = _appAsset.Size / 1_000_000.0;
            return await base.ShowDialogAsync();
        }


        protected override async Task CancelAsync()
        {
            await CancelDownload();
            await base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await CancelDownload();
            await base.CloseAsync();
        }


        private async Task Download()
        {
            try
            {
                ErrorMessage = string.Empty;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    _assetLocation = Path.Combine(_settings.DirectoryTemp, Path.GetFileName(_appAsset.Name));
                    await _downloadService.DownloadAsync(_appAsset.DownloadLink, _assetLocation, _updateProgress, _cancellationTokenSource.Token);
                    await SaveAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // download Canceled
            }
            catch (Exception)
            {
                ErrorMessage = "Failed to download update files\nIf problems persist please try again later";
            }
        }


        private Task CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
            return Task.CompletedTask;
        }


        private void UpdateProgress(FileDownloadProgress progress)
        {
            if (DateTime.UtcNow > _lastUpdate)
            {
                _lastUpdate = DateTime.UtcNow.AddMilliseconds(250);
                DownloadProgress = progress.TotalProgress;
                DownloadSpeed = progress.BytesSec > 0 ? (float)(progress.BytesSec / 1_000_000.0) : 0f;
                DownloadAmount = progress.TotalBytes > 0 ? (float)(progress.TotalBytes / 1_000_000.0) : 0f;
                DownloadRemaining = progress.GetRemainingTime();
            }
        }

    }
}
