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

        public DownloadQueueItem(int index, DiffusionModel diffusionModel, bool isVerify)
        {
            Index = index;
            IsVerify = isVerify;
            Progress = new ProgressInfo();
            TotalProgress = new ProgressInfo();
            DiffusionModel = diffusionModel;
            ProgressCallback = new Progress<PipelineProgress>(OnProgress);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public int Index { get; set; }
        public ProgressInfo Progress { get; }
        public ProgressInfo TotalProgress { get; }
        public IProgress<PipelineProgress> ProgressCallback { get; }
        public DiffusionModel DiffusionModel { get; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public ModelStatusType Status => DiffusionModel.Status;
        public string Name => DiffusionModel.Name;
        public string Pipeline => DiffusionModel.Pipeline;
        public bool IsVerify { get; set; }

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
