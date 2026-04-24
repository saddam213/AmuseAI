// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for MoveFolderDialog.xaml
    /// </summary>
    public partial class MoveFolderDialog : DialogControl
    {
        const double GB = 1024.0 * 1024 * 1024;
        private string _sourceDirectory;
        private string _destinationDirectory;
        private string _userDirectory;
        private string _folderName;
        private IProgress<MoveProgress> _progressCallback;
        private int _totalFiles;
        private int _movedFiles;
        private bool _isMoving;
        private double _totalSize;
        private double _movedSize;

        public MoveFolderDialog()
        {
            _progressCallback = new Progress<MoveProgress>(UpdateProgress);
            Progress = new ProgressInfo();
            MoveCommand = new AsyncRelayCommand(MoveAsync, CanExecuteMove);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanExecuteCancel);
            InitializeComponent();
        }

        public ProgressInfo Progress { get; }
        public AsyncRelayCommand MoveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public bool IsMoving
        {
            get { return _isMoving; }
            set { SetProperty(ref _isMoving, value); }
        }

        public int MovedFiles
        {
            get { return _movedFiles; }
            set { SetProperty(ref _movedFiles, value); }
        }

        public int TotalFiles
        {
            get { return _totalFiles; }
            set { SetProperty(ref _totalFiles, value); }
        }

        public double MovedSize
        {
            get { return _movedSize; }
            set { SetProperty(ref _movedSize, value); }
        }

        public double TotalSize
        {
            get { return _totalSize; }
            set { SetProperty(ref _totalSize, value); }
        }

        public string SourceDirectory
        {
            get { return _sourceDirectory; }
            set { SetProperty(ref _sourceDirectory, value); }
        }

        public string DestinationDirectory
        {
            get { return _destinationDirectory; }
            set { SetProperty(ref _destinationDirectory, value); }
        }

        public string UserDirectory
        {
            get { return _userDirectory; }
            set
            {
                SetProperty(ref _userDirectory, value);
                DestinationDirectory = Path.Combine(_userDirectory, _folderName);
            }
        }


        public Task<bool> ShowDialogAsync(string sourceDirectory, string foldername)
        {
            _folderName = foldername;
            Title = $"Move {_folderName}";
            SourceDirectory = sourceDirectory;
            foreach (var file in Directory.EnumerateFiles(_sourceDirectory, "*", SearchOption.AllDirectories).Select(x => new FileInfo(x)))
            {
                TotalFiles++;
                TotalSize += file.Length / GB;
            }
            return base.ShowDialogAsync();
        }


        private async Task MoveAsync()
        {
            IsMoving = true;

            await Task.Run(() => MoveDirectory(_sourceDirectory, _destinationDirectory));
            await base.SaveAsync();
            IsMoving = false;
        }


        private void MoveDirectory(string source, string destination)
        {
            var directory = new DirectoryInfo(source);
            if (!directory.Exists)
                return;

            Directory.CreateDirectory(destination);
            foreach (var file in directory.GetFiles())
            {
                file.MoveTo(Path.Combine(destination, file.Name), true);
                _progressCallback.Report(new MoveProgress(file.Name, file.Length));
            }

            foreach (var subDirectory in directory.GetDirectories())
            {
                MoveDirectory(subDirectory.FullName, Path.Combine(destination, subDirectory.Name));
            }
            Directory.Delete(source);
        }


        private void UpdateProgress(MoveProgress progress)
        {
            MovedFiles++;
            MovedSize += progress.Size / GB;
            Progress.Update(MovedFiles, TotalFiles, progress.File);
        }


        private bool CanExecuteMove()
        {
            return Directory.Exists(_userDirectory);
        }


        protected override bool CanExecuteCancel()
        {
            return !_isMoving;
        }


        protected override Task CloseAsync()
        {
            if (_isMoving)
                return Task.CompletedTask;

            return base.CloseAsync();
        }

        public record MoveProgress(string File, long Size);
    }

}
