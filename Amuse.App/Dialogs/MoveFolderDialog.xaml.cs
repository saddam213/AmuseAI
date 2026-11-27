// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Resources;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

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
        private string _destinationError;
        private readonly ILogger<MoveFolderDialog> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveFolderDialog"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MoveFolderDialog(ILogger<MoveFolderDialog> logger)
        {
            _logger = logger;
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

        public string DestinationError
        {
            get { return _destinationError; }
            set { SetProperty(ref _destinationError, value); }
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
                DestinationDirectory = string.IsNullOrWhiteSpace(_userDirectory) ? null : Path.Combine(_userDirectory, _folderName);
                DestinationError = GetDestinationError();
            }
        }


        /// <summary>
        /// Shows the dialog to move a folder to a new location.
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="folderName">The folder name created in the new location.</param>
        /// <param name="displayName">The localized folder name shown in the title.</param>
        public Task<bool> ShowDialogAsync(string sourceDirectory, string folderName, string displayName)
        {
            _folderName = folderName;
            Title = string.Format(AppDefault.MoveFolderTitle, displayName);
            SourceDirectory = sourceDirectory;
            if (!Directory.Exists(_sourceDirectory))
                Directory.CreateDirectory(_sourceDirectory);

            foreach (var file in Directory.EnumerateFiles(_sourceDirectory, "*", SearchOption.AllDirectories).Select(x => new FileInfo(x)))
            {
                TotalFiles++;
                TotalSize += file.Length / GB;
            }
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Moves the folder, the dialog stays open on an error so the user can choose another location or cancel.
        /// </summary>
        private async Task MoveAsync()
        {
            try
            {
                IsMoving = true;
                await Task.Run(() => MoveDirectory(_sourceDirectory, _destinationDirectory));
                IsMoving = false;
                await base.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MoveFolderDialog] [Move] Moving folder failed, Source: {Source}, Destination: {Destination}", _sourceDirectory, _destinationDirectory);
                IsMoving = false;
                await DialogService.ShowErrorAsync(AppErrors.MoveFolderErrorTitle, ex.Message);
            }
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


        /// <summary>
        /// Determines whether the folder can be moved to the chosen location.
        /// </summary>
        private bool CanExecuteMove()
        {
            return !_isMoving && Directory.Exists(_userDirectory) && _destinationError == null;
        }


        /// <summary>
        /// Gets why the folder can't be moved to the chosen location, <c>null</c> when it can.
        /// The new location can't be the folder or inside it, and can't already hold files of its own.
        /// </summary>
        private string GetDestinationError()
        {
            if (string.IsNullOrEmpty(_destinationDirectory) || string.IsNullOrEmpty(_sourceDirectory))
                return null;

            try
            {
                var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_sourceDirectory));
                var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_destinationDirectory));
                if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return AppErrors.MoveFolderInsideSource;

                if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
                    return string.Format(AppErrors.MoveFolderNotEmpty, destination);

                var destinationRoot = Path.GetPathRoot(destination);
                if (!string.Equals(destinationRoot, Path.GetPathRoot(source), StringComparison.OrdinalIgnoreCase) && GetFreeSpace(destinationRoot) < _totalSize)
                    return string.Format(AppErrors.MoveFolderNoSpace, _totalSize);

                return null;
            }
            catch (Exception ex)
            {
                // A location that can't be read or is not a valid path can't be moved to
                _logger.LogWarning(ex, "[MoveFolderDialog] [Validate] Checking destination failed, Destination: {Destination}", _destinationDirectory);
                return ex.Message;
            }
        }


        /// <summary>
        /// Gets the free space in GB of a drive, unlimited when it can't be read.
        /// </summary>
        /// <param name="driveRoot">The drive root.</param>
        private static double GetFreeSpace(string driveRoot)
        {
            try
            {
                return new DriveInfo(driveRoot).AvailableFreeSpace / GB;
            }
            catch (Exception)
            {
                return double.MaxValue;
            }
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

        private sealed record MoveProgress(string File, long Size);
    }

}
