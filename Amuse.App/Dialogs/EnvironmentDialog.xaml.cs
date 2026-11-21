// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for EnvironmentDialog.xaml
    /// </summary>
    public partial class EnvironmentDialog : DialogControl
    {
        private readonly IEnvironmentService _environmentService;
        private readonly IGenerateService _generateService;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private bool _isExecuting;
        private PipelineModel _pipeline;
        private EnvironmentModel _environment;
        private readonly CancellationTokenSource _cancellation;
        private string _message;
        private string _subMessage;
        private string _waitMessage;
        private string _downloadPercent;
        private string _downloadSize;
        private string _downloadSpeed;
        private string _downloadRemaining;
        private EnvironmentSetupItem _setupItem;

        public EnvironmentDialog(IEnvironmentService environmentService, IGenerateService generateService)
        {
            _cancellation = new CancellationTokenSource();
            _environmentService = environmentService;
            _generateService = generateService;
            _progressCallback = new Progress<PipelineProgress>(OnProgressUpdate);
            CancelCommand = new AsyncRelayCommand(CloseAsync);
            CreateCommand = new AsyncRelayCommand(CreateEnvironment);
            UpdateCommand = new AsyncRelayCommand(UpdateEnvironment);
            RebuildCommand = new AsyncRelayCommand(RebuildEnvironment);
            Progress = new ProgressInfo();
            InitializeComponent();
        }

        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand CreateCommand { get; }
        public AsyncRelayCommand UpdateCommand { get; }
        public AsyncRelayCommand RebuildCommand { get; }
        public ProgressInfo Progress { get; set; }
        public bool IsCreate { get; set; }
        public bool IsUpdate { get; set; }
        public bool IsRebuild { get; set; }

        public bool IsExecuting
        {
            get { return _isExecuting; }
            set { SetProperty(ref _isExecuting, value); }
        }

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        public string SubMessage
        {
            get { return _subMessage; }
            set { SetProperty(ref _subMessage, value); }
        }

        public string WaitMessage
        {
            get { return _waitMessage; }
            set { SetProperty(ref _waitMessage, value); }
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

        public EnvironmentSetupItem SetupItem
        {
            get { return _setupItem; }
            set { SetProperty(ref _setupItem, value); NotifyPropertyChanged(nameof(HasOptions)); }
        }

        public bool HasOptions => _setupItem?.Options.Count > 1;

        /// <summary>
        /// Gets the environment that was set up, the one the user chose when options are shown.
        /// </summary>
        public EnvironmentModel Environment => _setupItem?.Environment ?? _environment;


        private void SetMessages(BackendType backendType)
        {
            var backendName = GetBackendName(backendType);
            if (IsCreate)
            {
                Message = $"Create {backendName} Environment?";
                SubMessage = $"This will create the required {backendName} Environment on your machine for running local models, this is a one-time setup.";
                WaitMessage = "(This may take several minutes)";
            }
            if (IsUpdate)
            {
                Message = $"Update {backendName} Environment?";
                SubMessage = $"This will update your {backendName} Environment with the latest required packages, This is a one-off process.";
                WaitMessage = "(This may take few minutes)";
            }
            if (IsRebuild)
            {
                Message = $"Rebuild {backendName} Environment?";
                SubMessage = $"This will completely wipe and reinstall your {backendName} Environment to fix any broken dependencies or corrupted files";
                WaitMessage = "(This may take several minutes)";
            }
        }


        private string GetBackendName(BackendType backendType)
        {
            return backendType switch
            {
                BackendType.HuggingFace => "Python Virtual",
                BackendType.StableDiffusionCpp => "StableDiffusion.cpp",
                BackendType.AudioCpp => "Audio.cpp",
                BackendType.LlamaCpp => "Llama.cpp",
                BackendType.OnnxRuntime => "OnnxRuntime",
                _ => "Virtual"
            };
        }


        public Task<bool> CreateAsync(PipelineModel pipeline)
        {
            IsCreate = true;
            _pipeline = pipeline;
            NotifyPropertyChanged(nameof(IsCreate));
            SetMessages(pipeline.Backend);
            return base.ShowDialogAsync();
        }


        public Task<bool> CreateAsync(EnvironmentModel environment)
        {
            return CreateAsync(environment, false);
        }


        /// <summary>
        /// Shows the dialog to create an environment.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="isSelectable">if set to <c>true</c> the user can choose any environment in the same group, the chosen one becomes the default.</param>
        public Task<bool> CreateAsync(EnvironmentModel environment, bool isSelectable)
        {
            IsCreate = true;
            _environment = environment;
            if (isSelectable)
                SetupItem = new EnvironmentSetupItem(environment.Backend, environment.Vendor, _environmentService.GetEnvironmentGroup(environment), false, true, _environmentService.Exists);

            NotifyPropertyChanged(nameof(IsCreate));
            SetMessages(environment.Backend);
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(EnvironmentModel environment)
        {
            IsUpdate = true;
            _environment = environment;
            NotifyPropertyChanged(nameof(IsUpdate));
            SetMessages(environment.Backend);
            return base.ShowDialogAsync();
        }


        public Task<bool> RebuildAsync(EnvironmentModel environment)
        {
            IsRebuild = true;
            _environment = environment;
            NotifyPropertyChanged(nameof(IsRebuild));
            SetMessages(environment.Backend);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Create an new environment
        /// </summary>
        private async Task CreateEnvironment()
        {
            IsExecuting = true;
            try
            {
                if (_generateService.IsLoaded)
                    await _generateService.UnloadAsync();

                if (_pipeline != null)
                    await _environmentService.CreateAsync(_pipeline, _progressCallback, _cancellation.Token);

                if (_setupItem != null)
                {
                    // Chosen, install it unless already installed, then make it the default so inference uses it
                    if (!_environmentService.Exists(_setupItem.Environment))
                        await _environmentService.CreateAsync(_setupItem.Environment, _progressCallback, _cancellation.Token);

                    await _environmentService.SetDefaultAsync(_setupItem.Environment);
                }
                else if (_environment != null)
                {
                    await _environmentService.CreateAsync(_environment, _progressCallback, _cancellation.Token);
                }
                await base.SaveAsync();
            }
            catch (OperationCanceledException)
            {
                await base.CloseAsync();
            }
        }


        /// <summary>
        /// Updates an existing environment
        /// </summary>
        private async Task UpdateEnvironment()
        {
            IsExecuting = true;
            try
            {
                if (_generateService.IsLoaded)
                    await _generateService.UnloadAsync();

                await _environmentService.UpdateAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException)
            {
                await base.CloseAsync();
            }
        }


        /// <summary>
        /// Rebuild an existing environment
        /// </summary>
        private async Task RebuildEnvironment()
        {
            IsExecuting = true;
            try
            {
                if (_generateService.IsLoaded)
                    await _generateService.UnloadAsync();

                await _environmentService.RebuildAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException)
            {
                await base.CloseAsync();
            }
        }


        protected override async Task CloseAsync()
        {
            await _cancellation.CancelAsync();
            await base.CloseAsync();
        }


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
            DownloadPercent = null;
            DownloadSize = null;
            DownloadSpeed = null;
            DownloadRemaining = null;
            Progress.Indeterminate(progress.Message);
        }
    }
}
