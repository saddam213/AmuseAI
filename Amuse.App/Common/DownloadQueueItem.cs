using System;
using System.Threading;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Common
{
    public sealed class DownloadQueueItem : BaseModel
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private float _speed;
        private string _description;
        private string _errorMessage;
        private string _remaining;
        private DateTime _lastUpdate;

        public DownloadQueueItem(int index, IDownloadModel model)
        {
            Index = index;
            DownloadModel = model;
            Progress = new ProgressInfo();
            TotalProgress = new ProgressInfo();
            ProgressCallback = new Progress<DownloadProgress>(OnProgress);
            _description = GetDescription();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public int Index { get; set; }
        public ProgressInfo Progress { get; }
        public ProgressInfo TotalProgress { get; }
        public IProgress<DownloadProgress> ProgressCallback { get; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public ModelStatusType Status => DownloadModel.Status;
        public string Name => DownloadModel?.Name;
        public string Description => _description;
        public IDownloadModel DownloadModel { get; }

        public float Speed
        {
            get { return _speed; }
            set { SetProperty(ref _speed, value); }
        }

        public string Remaining
        {
            get { return _remaining; }
            set { SetProperty(ref _remaining, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public void Cancel()
        {
            ErrorMessage = null;
            _cancellationTokenSource.SafeCancel();
        }


        public void UpdateStatus(ModelStatusType status)
        {
            if (DownloadModel != null)
                DownloadModel.Status = status;

            NotifyPropertyChanged(nameof(Status));
        }


        private void OnProgress(DownloadProgress progress)
        {
            if ((DateTime.UtcNow > _lastUpdate))
            {
                _lastUpdate = DateTime.UtcNow.AddMilliseconds(500);
                Remaining = GetRemainingTime(progress);
                Speed = progress.BytesSec > 0 ? (float)(progress.BytesSec / 1_000_000.0) : 0f;
            }

            Progress.Update((int)progress.FileProgress, 100);
            TotalProgress.Update((int)progress.TotalProgress, 100);
        }


        private string GetDescription()
        {
            try
            {
                if (DownloadModel is ComponentModel)
                    return "Component";

                return $"{DownloadModel.GetType().GetProperty("Pipeline").GetValue(DownloadModel)}";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }


        private static string GetRemainingTime(DownloadProgress progress)
        {
            var bytesLeft = progress.TotalSize - progress.TotalBytes;
            if (bytesLeft <= 0)
                return "Complete";

            if (progress.BytesSec == 0)
                return "Calculating...";

            var secondsLeft = bytesLeft / progress.BytesSec;
            var timeSpan = TimeSpan.FromSeconds(secondsLeft);
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours}h remaining";
            else if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m remaining";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s remaining";

            return $"{timeSpan.Seconds}s remaining";
        }
    }
}
