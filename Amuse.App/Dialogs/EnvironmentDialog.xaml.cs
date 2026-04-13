// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Python.Common;
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
        private readonly IDiffusionService _diffusionService;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private bool _isExecuting;
        private PipelineModel _pipeline;
        private EnvironmentModel _environment;
        private readonly CancellationTokenSource _cancellation;

        public EnvironmentDialog(IEnvironmentService environmentService, IDiffusionService diffusionService)
        {
            _cancellation = new CancellationTokenSource();
            _environmentService = environmentService;
            _diffusionService = diffusionService;
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


        public Task<bool> CreateAsync(PipelineModel pipeline)
        {
            IsCreate = true;
            _pipeline = pipeline;
            NotifyPropertyChanged(nameof(IsCreate));
            return base.ShowDialogAsync();
        }


        public Task<bool> CreateAsync(EnvironmentModel environment)
        {
            IsCreate = true;
            _environment = environment;
            NotifyPropertyChanged(nameof(IsCreate));
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(EnvironmentModel environment)
        {
            IsUpdate = true;
            _environment = environment;
            NotifyPropertyChanged(nameof(IsUpdate));
            return base.ShowDialogAsync();
        }


        public Task<bool> RebuildAsync(EnvironmentModel environment)
        {
            IsRebuild = true;
            _environment = environment;
            NotifyPropertyChanged(nameof(IsRebuild));
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
                if (_diffusionService.IsLoaded)
                    await _diffusionService.UnloadAsync();

                if (_pipeline != null)
                    await _environmentService.CreateAsync(_pipeline, _progressCallback, _cancellation.Token);
                if (_environment != null)
                    await _environmentService.CreateAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException) { }
        }


        /// <summary>
        /// Updates an existing environment
        /// </summary>
        private async Task UpdateEnvironment()
        {
            IsExecuting = true;
            try
            {
                if (_diffusionService.IsLoaded)
                    await _diffusionService.UnloadAsync();

                await _environmentService.UpdateAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException) { }
           
        }


        /// <summary>
        /// Rebuild an existing environment
        /// </summary>
        private async Task RebuildEnvironment()
        {
            IsExecuting = true;
            try
            {
                if (_diffusionService.IsLoaded)
                    await _diffusionService.UnloadAsync();

                await _environmentService.RebuildAsync(_environment, _progressCallback, _cancellation.Token);
                await base.SaveAsync();
            }
            catch (OperationCanceledException) { }
        }


        protected override async Task CloseAsync()
        {
            await _cancellation.CancelAsync();
            await base.CloseAsync();
        }


        private void OnProgressUpdate(PipelineProgress progress)
        {
            Progress.Indeterminate(progress.Message);
        }
    }
}
