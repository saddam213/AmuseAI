// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    public partial class AppSetupDialog : DialogControl
    {
        private readonly Settings _settings;
        private readonly IEnvironmentService _environmentService;
        private readonly IModelAccessService _modelAccessService;
        private readonly IMigrationService _migrationService;
        private readonly ILogger<AppSetupDialog> _logger;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private readonly CancellationTokenSource _cancellation;
        private bool _isExecuting;
        private bool _isComplete;
        private bool _isDontAskAgain;
        private bool _isManage;
        private string _downloadPercent;
        private string _downloadSize;
        private string _downloadSpeed;
        private string _downloadRemaining;
        private int _environmentColumns;
        private bool _isFirstRun;
        private string _modelDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppSetupDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="modelAccessService">The model access service.</param>
        /// <param name="migrationService">The migration service.</param>
        /// <param name="logger">The logger.</param>
        public AppSetupDialog(Settings settings, IEnvironmentService environmentService, IModelAccessService modelAccessService, IMigrationService migrationService, ILogger<AppSetupDialog> logger)
        {
            _settings = settings;
            _environmentService = environmentService;
            _modelAccessService = modelAccessService;
            _migrationService = migrationService;
            _logger = logger;
            _cancellation = new CancellationTokenSource();
            _progressCallback = new Progress<PipelineProgress>(OnProgressUpdate);
            Items = new ObservableCollection<EnvironmentSetupItem>();
            Groups = new ObservableCollection<EnvironmentSetupGroup>();
            Progress = new ProgressInfo();
            InstallCommand = new AsyncRelayCommand(InstallAsync, CanInstall);
            LaterCommand = new AsyncRelayCommand(LaterAsync);
            BrowseModelDirectoryCommand = new AsyncRelayCommand(BrowseModelDirectoryAsync);
            InitializeComponent();
        }

        public Settings Settings => _settings;
        public AsyncRelayCommand BrowseModelDirectoryCommand { get; }
        public ObservableCollection<EnvironmentSetupItem> Items { get; }
        public ObservableCollection<EnvironmentSetupGroup> Groups { get; }
        public ProgressInfo Progress { get; }
        public IReadOnlyList<ScaleOption> ScaleOptions => ScaleOption.Options;
        public AsyncRelayCommand InstallCommand { get; }
        public AsyncRelayCommand LaterCommand { get; }

        public bool IsExecuting
        {
            get { return _isExecuting; }
            set { SetProperty(ref _isExecuting, value); }
        }

        public bool IsComplete
        {
            get { return _isComplete; }
            set { SetProperty(ref _isComplete, value); }
        }

        public string DownloadPercent
        {
            get { return _downloadPercent; }
            set { SetProperty(ref _downloadPercent, value); }
        }

        public string DownloadSize
        {
            get { return _downloadSize; }
            set { SetProperty(ref _downloadSize, value); }
        }

        public string DownloadSpeed
        {
            get { return _downloadSpeed; }
            set { SetProperty(ref _downloadSpeed, value); }
        }

        public string DownloadRemaining
        {
            get { return _downloadRemaining; }
            set { SetProperty(ref _downloadRemaining, value); }
        }

        public bool IsDontAskAgain
        {
            get { return _isDontAskAgain; }
            set { SetProperty(ref _isDontAskAgain, value); }
        }

        public bool IsManage
        {
            get { return _isManage; }
            set { SetProperty(ref _isManage, value); }
        }

        public int EnvironmentColumns
        {
            get { return _environmentColumns; }
            set { SetProperty(ref _environmentColumns, value); }
        }

        public bool IsFirstRun
        {
            get { return _isFirstRun; }
            set { SetProperty(ref _isFirstRun, value); }
        }

        public string ModelDirectory
        {
            get { return _modelDirectory; }
            set { SetProperty(ref _modelDirectory, value); }
        }


        /// <summary>
        /// Gets a value indicating whether any backend has no installed environment on startup, the don't ask again option only applies to these.
        /// </summary>
        public bool HasMissingEnvironments => !IsManage && Items.Any(x => !x.IsUpdateRequired);


        /// <summary>
        /// Gets a value indicating whether the first run has no environment selected, so there are only the first time settings to save.
        /// </summary>
        public bool IsSaveOnly => IsFirstRun && !Items.Any(x => x.IsSelected);


        /// <summary>
        /// Shows the dialog with the backends to set up on startup, the first run also shows the first time settings.
        /// </summary>
        /// <param name="items">The environments that need an update, and the backends with no installed environment.</param>
        public Task<bool> ShowDialogAsync(IReadOnlyList<EnvironmentSetupItem> items)
        {
            IsFirstRun = !_settings.IsFirstRunComplete;
            ModelDirectory = IsFirstRun
                ? GetSuggestedModelDirectory(_settings.DirectoryModel, App.DirectoryData, App.DirectoryBase)
                : _settings.DirectoryModel;
            LoadItems(items);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog with every backend, installed or not, so the user can install or try other environments.
        /// </summary>
        public override Task<bool> ShowDialogAsync()
        {
            IsManage = true;
            LoadItems(_environmentService.GetSetupEnvironments(true, true));
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Loads the items and groups them by backend.
        /// </summary>
        /// <param name="items">The items.</param>
        private void LoadItems(IReadOnlyList<EnvironmentSetupItem> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }

            // Groups, one section per backend with its devices stacked
            foreach (var group in items.GroupBy(x => x.Backend))
                Groups.Add(new EnvironmentSetupGroup(group.Key, [.. group]));

            EnvironmentColumns = items.Select(x => x.Environments.Count).DefaultIfEmpty(1).Max();
            NotifyPropertyChanged(nameof(HasMissingEnvironments));
            NotifyPropertyChanged(nameof(IsSaveOnly));
        }


        /// <summary>
        /// Installs or updates the selected environments, and makes each installed environment the default, with none selected on the first run only the first time settings are saved.
        /// </summary>
        private async Task InstallAsync()
        {
            if (!await SaveFirstRunAsync())
                return;

            IsExecuting = true;
            var selectedItems = Items.Where(x => x.IsSelected).ToList();
            try
            {
                // Don't Ask
                await SaveDontAskAsync();

                // Unload, the loaded pipeline may be using an environment that is being installed or replaced as the default
                foreach (var item in selectedItems)
                {
                    if (!await _modelAccessService.RequestEnvironmentReleaseAsync(item.Environment))
                        throw new OperationCanceledException();
                }

                // Install
                foreach (var item in selectedItems)
                {
                    _cancellation.Token.ThrowIfCancellationRequested();
                    await InstallItemAsync(item);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[AppSetup] [Install] Install cancelled.");
                await base.CloseAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppSetup] [Install] Install failed.");
                IsExecuting = false;
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
                return;
            }
            finally
            {
                IsExecuting = false;
            }

            // Result
            if (selectedItems.All(x => x.Status == EnvironmentSetupStatus.Installed))
            {
                await base.SaveAsync();
                return;
            }

            Progress.Clear();
            ClearDownloadProgress();
            IsComplete = true;
        }


        /// <summary>
        /// Installs the environment of a single backend.
        /// </summary>
        /// <param name="item">The item.</param>
        private async Task InstallItemAsync(EnvironmentSetupItem item)
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                _logger.LogInformation("[AppSetup] [Install] Environment {Name}, IsUpdate: {IsUpdateRequired}...", item.Environment.Name, item.IsUpdateRequired);
                item.Status = EnvironmentSetupStatus.Installing;
                if (item.IsUpdateRequired)
                {
                    await _environmentService.UpdateAsync(item.Environment, _progressCallback, _cancellation.Token);
                }
                else
                {
                    // Already installed environments only need to become the default
                    if (!_environmentService.Exists(item.Environment))
                        await _environmentService.CreateAsync(item.Environment, _progressCallback, _cancellation.Token);

                    await _environmentService.SetDefaultAsync(item.Environment);
                }

                item.Status = EnvironmentSetupStatus.Installed;
                _logger.LogInformation("[AppSetup] [Install] Environment {Name}, IsUpdate: {IsUpdateRequired} complete, Elapsed: {Elapsed:c}", item.Environment.Name, item.IsUpdateRequired, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                item.Status = EnvironmentSetupStatus.Pending;
                throw;
            }
            catch (Exception ex)
            {
                // Keep going, the other backends do not depend on this one
                item.Status = EnvironmentSetupStatus.Failed;
                _logger.LogError(ex, "[AppSetup] [Install] An exception occurred installing environment {Name}", item.Environment.Name);
            }
        }


        /// <summary>
        /// Closes the dialog, or cancels the install when one is running.
        /// </summary>
        private async Task LaterAsync()
        {
            if (IsExecuting)
            {
                await _cancellation.CancelAsync();
                return;
            }

            if (!await SaveFirstRunAsync())
                return;

            await SaveDontAskAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Chooses the location of the model folder, a Models folder is created in the location unless it is one already.
        /// </summary>
        private async Task BrowseModelDirectoryAsync()
        {
            var directory = await DialogService.OpenFolderAsync(AppDefault.SelectModelFolder, ModelDirectory);
            if (string.IsNullOrEmpty(directory))
                return;

            var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
            ModelDirectory = string.Equals(folderName, "Models", StringComparison.OrdinalIgnoreCase)
                ? directory
                : Path.Combine(directory, "Models");
        }


        /// <summary>
        /// Gets the model folder suggested on the first run. An installed app defaults to a folder in the local application data, which is hard to find,
        /// so a folder on the internal drive with the most free space is suggested instead.
        /// The current folder is kept when the app is portable, when it is not the default folder, or when it already holds files.
        /// </summary>
        /// <param name="currentDirectory">The current model directory.</param>
        /// <param name="dataDirectory">The application data directory.</param>
        /// <param name="baseDirectory">The application directory, a portable app keeps its data here.</param>
        private static string GetSuggestedModelDirectory(string currentDirectory, string dataDirectory, string baseDirectory)
        {
            try
            {
                var defaultDirectory = Path.Combine(dataDirectory, "Models");
                if (IsSameDirectory(dataDirectory, baseDirectory) || !IsSameDirectory(currentDirectory, defaultDirectory))
                    return currentDirectory;

                if (Directory.Exists(currentDirectory) && Directory.EnumerateFiles(currentDirectory, "*", SearchOption.AllDirectories).Any())
                    return currentDirectory;

                var drive = DriveInfo.GetDrives()
                    .Where(x => x.DriveType == DriveType.Fixed && x.IsReady)
                    .OrderByDescending(x => x.AvailableFreeSpace)
                    .FirstOrDefault();
                if (drive == null)
                    return currentDirectory;

                return Path.Combine(drive.RootDirectory.FullName, "Amuse", "Models");
            }
            catch (Exception)
            {
                return currentDirectory;
            }
        }


        /// <summary>
        /// Determines whether two paths are the same directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="other">The other directory.</param>
        private static bool IsSameDirectory(string directory, string other)
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(other)),
                StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Saves the first time settings, so they are only shown on the first run.
        /// </summary>
        /// <returns><c>true</c> if the settings were saved or there is nothing to save, <c>false</c> if the model folder could not be created.</returns>
        private async Task<bool> SaveFirstRunAsync()
        {
            if (!IsFirstRun || _settings.IsFirstRunComplete)
                return true;

            var currentDirectory = _settings.DirectoryModel;
            try
            {
                if (!string.Equals(ModelDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.SetModelDirectory(ModelDirectory);
                    _settings.NotifyPropertyChanged(nameof(Settings.DirectoryModel));
                    await _migrationService.RunMigrationsAsync();
                }

                _settings.IsFirstRunComplete = true;
                await _settings.SaveAsync();
                _logger.LogInformation("[AppSetup] [FirstRun] First time settings saved, ModelDirectory: {ModelDirectory}, Language: {Language}, UIScale: {UIScale}", _settings.DirectoryModel, _settings.UILanguage, _settings.UIScale);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppSetup] [FirstRun] An exception occurred saving the first time settings, ModelDirectory: {ModelDirectory}", ModelDirectory);
                _settings.SetModelDirectory(currentDirectory);
                ModelDirectory = currentDirectory;
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Determines whether the selected environments can be installed.
        /// </summary>
        private bool CanInstall()
        {
            return !IsExecuting && !IsComplete && (IsSaveOnly || Items.Any(x => x.IsSelected));
        }


        /// <summary>
        /// Saves the don't ask again choice, so startup stops asking to install missing environments.
        /// </summary>
        private async Task SaveDontAskAsync()
        {
            if (!IsDontAskAgain || _settings.IsAppSetupAcknowledged)
                return;

            _logger.LogInformation("[AppSetup] [DontAsk] Missing environments will no longer be shown on startup.");
            _settings.IsAppSetupAcknowledged = true;
            await _settings.SaveAsync();
        }


        /// <summary>
        /// Closes the dialog, cancelling any running install.
        /// </summary>
        protected override async Task CloseAsync()
        {
            await _cancellation.CancelAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Handles a change to an item, refreshing the install command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EnvironmentSetupItem.IsSelected))
            {
                NotifyPropertyChanged(nameof(IsSaveOnly));
                InstallCommand.RaiseCanExecuteChanged();
            }
        }


        /// <summary>
        /// Handles a progress update from the environment install.
        /// </summary>
        /// <param name="progress">The progress.</param>
        private void OnProgressUpdate(PipelineProgress progress)
        {
            // Download
            if (progress.IsDownload())
            {
                if (progress.BatchMaximum > 0)
                    Progress.Update(progress.BatchValue, progress.BatchMaximum, progress.Message);
                else
                    Progress.Indeterminate(progress.Message);

                DownloadPercent = progress.GetDownloadPercentText();
                DownloadSize = progress.GetDownloadSizeText();
                DownloadSpeed = progress.GetDownloadSpeedText();
                DownloadRemaining = progress.GetDownloadRemainingText();
                return;
            }

            // Message
            ClearDownloadProgress();
            Progress.Indeterminate(progress.Message);
        }


        /// <summary>
        /// Clears the download progress text.
        /// </summary>
        private void ClearDownloadProgress()
        {
            DownloadPercent = null;
            DownloadSize = null;
            DownloadSpeed = null;
            DownloadRemaining = null;
        }
    }
}
