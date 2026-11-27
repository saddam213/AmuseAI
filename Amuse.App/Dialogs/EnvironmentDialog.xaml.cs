// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for EnvironmentDialog.xaml
    /// </summary>
    public partial class EnvironmentDialog : DialogControl
    {
        private readonly IEnvironmentService _environmentService;
        private readonly IModelAccessService _modelAccessService;
        private readonly ILogger<EnvironmentDialog> _logger;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private bool _isExecuting;
        private PipelineModel _loadingPipeline;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentDialog"/> class.
        /// </summary>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="modelAccessService">The model access service.</param>
        /// <param name="logger">The logger.</param>
        public EnvironmentDialog(IEnvironmentService environmentService, IModelAccessService modelAccessService, ILogger<EnvironmentDialog> logger)
        {
            _logger = logger;
            _cancellation = new CancellationTokenSource();
            _environmentService = environmentService;
            _modelAccessService = modelAccessService;
            _progressCallback = new Progress<PipelineProgress>(OnProgressUpdate);
            CancelCommand = new AsyncRelayCommand(CloseAsync);
            CreateCommand = new AsyncRelayCommand(CreateEnvironment);
            UpdateCommand = new AsyncRelayCommand(UpdateEnvironment);
            Progress = new ProgressInfo();
            InitializeComponent();
        }

        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand CreateCommand { get; }
        public AsyncRelayCommand UpdateCommand { get; }
        public ProgressInfo Progress { get; set; }
        public bool IsCreate { get; set; }
        public bool IsUpdate { get; set; }

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
                Message = string.Format(AppDefault.EnvironmentCreateMessage, backendName);
                SubMessage = string.Format(AppDefault.EnvironmentCreateSubMessage, backendName);
                WaitMessage = AppDefault.EnvironmentSetupWaitMessage;
            }
            if (IsUpdate)
            {
                Message = string.Format(AppDefault.EnvironmentUpdateMessage, backendName);
                SubMessage = string.Format(AppDefault.EnvironmentUpdateSubMessage, backendName);
                WaitMessage = AppDefault.EnvironmentUpdateWaitMessage;
            }
        }


        private string GetBackendName(BackendType backendType)
        {
            return backendType switch
            {
                BackendType.StableDiffusionCpp => "StableDiffusion.cpp",
                BackendType.AudioCpp => "Audio.cpp",
                BackendType.LlamaCpp => "Llama.cpp",
                BackendType.OnnxRuntime => "OnnxRuntime",
                _ => AppDefault.EnvironmentNameVirtual
            };
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
        /// <param name="loadingPipeline">The pipeline that is loading and needs the environment, its own loaded models are unloaded without asking.</param>
        public Task<bool> CreateAsync(EnvironmentModel environment, bool isSelectable, PipelineModel loadingPipeline = null)
        {
            IsCreate = true;
            _environment = environment;
            _loadingPipeline = loadingPipeline;
            if (isSelectable)
                SetupItem = new EnvironmentSetupItem(environment.Backend, environment.Vendor, _environmentService.GetEnvironmentGroup(environment), false, true, _environmentService.Exists);

            NotifyPropertyChanged(nameof(IsCreate));
            SetMessages(environment.Backend);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to update an environment.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="loadingPipeline">The pipeline that is loading and needs the environment, its own loaded models are unloaded without asking.</param>
        public Task<bool> UpdateAsync(EnvironmentModel environment, PipelineModel loadingPipeline = null)
        {
            IsUpdate = true;
            _environment = environment;
            _loadingPipeline = loadingPipeline;
            NotifyPropertyChanged(nameof(IsUpdate));
            SetMessages(environment.Backend);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Create an new environment, choosing one that is already installed only makes it the default, so nothing is unloaded.
        /// </summary>
        private async Task CreateEnvironment()
        {
            try
            {
                var isInstallRequired = !_environmentService.Exists(Environment);
                if (isInstallRequired && !await _modelAccessService.RequestEnvironmentReleaseAsync(Environment, _loadingPipeline))
                {
                    await base.CloseAsync();
                    return;
                }

                IsExecuting = true;
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
                _logger.LogInformation("[EnvironmentDialog] [Create] Creating environment cancelled");
                await base.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnvironmentDialog] [Create] Creating environment failed");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
                await base.CloseAsync();
            }
        }


        /// <summary>
        /// Updates an existing environment
        /// </summary>
        private async Task UpdateEnvironment()
        {
            try
            {
                if (!await _modelAccessService.RequestEnvironmentReleaseAsync(_environment, _loadingPipeline))
                {
                    await base.CloseAsync();
                    return;
                }

                IsExecuting = true;
                await _environmentService.UpdateAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[EnvironmentDialog] [Update] Updating environment cancelled");
                await base.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnvironmentDialog] [Update] Updating environment failed");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
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
