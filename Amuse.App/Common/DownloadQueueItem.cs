using Amuse.App.Resources;
using System;
using System.Threading;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Common
{
    public sealed class DownloadQueueItem : BaseModel
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private float _speed;
        private string _description;
        private string _errorMessage;
        private bool _isFailed;
        private string _remaining;
        private DateTime _lastUpdate;

        public DownloadQueueItem(int index, IDownloadModel model)
        {
            Index = index;
            DownloadModel = model;
            Progress = new ProgressInfo();
            TotalProgress = new ProgressInfo();
            ProgressCallback = new Progress<FileDownloadProgress>(OnProgress);
            _description = GetDescription();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public int Index { get; set; }
        public ProgressInfo Progress { get; }
        public ProgressInfo TotalProgress { get; }
        public IProgress<FileDownloadProgress> ProgressCallback { get; }
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

        /// <summary>
        /// Gets or sets a value indicating whether the download failed, kept on the item as the model status can change while it waits to be retried.
        /// </summary>
        public bool IsFailed
        {
            get { return _isFailed; }
            set { SetProperty(ref _isFailed, value); }
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


        private void OnProgress(FileDownloadProgress progress)
        {
            if ((DateTime.UtcNow > _lastUpdate))
            {
                _lastUpdate = DateTime.UtcNow.AddMilliseconds(500);
                Remaining = progress.GetRemainingTime();
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
                    return AppDefault.Component;

                return $"{DownloadModel.GetType().GetProperty("Pipeline").GetValue(DownloadModel)}";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
