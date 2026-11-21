// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    public partial class AppSetupDialog : DialogControl
    {
        private readonly Settings _settings;
        private readonly IEnvironmentService _environmentService;
        private readonly IGenerateService _generateService;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AppSetupDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="generateService">The generate service.</param>
        /// <param name="logger">The logger.</param>
        public AppSetupDialog(Settings settings, IEnvironmentService environmentService, IGenerateService generateService, ILogger<AppSetupDialog> logger)
        {
            _settings = settings;
            _environmentService = environmentService;
            _generateService = generateService;
            _logger = logger;
            _cancellation = new CancellationTokenSource();
            _progressCallback = new Progress<PipelineProgress>(OnProgressUpdate);
            Items = new ObservableCollection<EnvironmentSetupItem>();
            Groups = new ObservableCollection<EnvironmentSetupGroup>();
            Progress = new ProgressInfo();
            InstallCommand = new AsyncRelayCommand(InstallAsync, CanInstall);
            LaterCommand = new AsyncRelayCommand(LaterAsync);
            InitializeComponent();
        }

        public ObservableCollection<EnvironmentSetupItem> Items { get; }
        public ObservableCollection<EnvironmentSetupGroup> Groups { get; }
        public ProgressInfo Progress { get; }
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


        /// <summary>
        /// Gets a value indicating whether any backend has no installed environment on startup, the don't ask again option only applies to these.
        /// </summary>
        public bool HasMissingEnvironments => !IsManage && Items.Any(x => !x.IsUpdateRequired);


        /// <summary>
        /// Shows the dialog with the backends to set up on startup.
        /// </summary>
        /// <param name="items">The environments that need an update, and the backends with no installed environment.</param>
        public Task<bool> ShowDialogAsync(IReadOnlyList<EnvironmentSetupItem> items)
        {
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

            EnvironmentColumns = Math.Max(1, items.Max(x => x.Environments.Count));
            NotifyPropertyChanged(nameof(HasMissingEnvironments));
        }


        /// <summary>
        /// Installs or updates the selected environments, and makes each installed environment the default.
        /// </summary>
        private async Task InstallAsync()
        {
            IsExecuting = true;
            var selectedItems = Items.Where(x => x.IsSelected).ToList();
            try
            {
                // Don't Ask
                await SaveDontAskAsync();

                // Unload, the loaded pipeline may be using an environment that is being installed or replaced as the default
                if (_generateService.IsLoaded)
                    await _generateService.UnloadAsync();

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

            await SaveDontAskAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Determines whether the selected environments can be installed.
        /// </summary>
        private bool CanInstall()
        {
            return !IsExecuting && !IsComplete && Items.Any(x => x.IsSelected);
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
                InstallCommand.RaiseCanExecuteChanged();
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
