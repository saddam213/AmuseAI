using System;
using System.Threading;
using TensorStack.Common;
using TensorStack.Python.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Common
{
    public class DownloadQueueItem : BaseModel
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private float _speed;
        private string _component;
        private string _fileName;

        public DownloadQueueItem(int index, IDownloadModel model, bool isVerify)
        {
            Index = index;
            IsVerify = isVerify;
            DownloadModel = model;
            Progress = new ProgressInfo();
            TotalProgress = new ProgressInfo();
            ProgressCallback = new Progress<PipelineProgress>(OnProgress);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public int Index { get; set; }
        public ProgressInfo Progress { get; }
        public ProgressInfo TotalProgress { get; }
        public IProgress<PipelineProgress> ProgressCallback { get; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public ModelStatusType Status => DownloadModel.Status;
        public string Name => DownloadModel?.Name;
        public string Pipeline => DownloadModel?.Pipeline;
        public bool IsVerify { get; set; }
        public IDownloadModel DownloadModel { get; }

        public float Speed
        {
            get { return _speed; }
            set { SetProperty(ref _speed, value); }
        }

        public string Component
        {
            get { return _component; }
            set { SetProperty(ref _component, value); }
        }

        public string FileName
        {
            get { return _fileName; }
            set { SetProperty(ref _fileName, value); }
        }


        public void Cancel()
        {
            _cancellationTokenSource.SafeCancel();
        }


        public void UpdateStatus(ModelStatusType status)
        {
            if (DownloadModel != null)
                DownloadModel.Status = status;

            NotifyPropertyChanged(nameof(Status));
        }


        private void OnProgress(PipelineProgress progress)
        {
            if (progress.Key?.Equals("Download") == false)
                return;

            Speed = progress.Elapsed;
            Component = progress.Subkey;
            FileName = progress.Message;
            Progress.Update(progress.Value, progress.Maximum);
            TotalProgress.Update(progress.BatchValue, progress.BatchMaximum);
        }

    }
}
