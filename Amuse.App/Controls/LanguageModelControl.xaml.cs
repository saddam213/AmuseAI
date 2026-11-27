using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for TextModelControl.xaml
    /// </summary>
    public partial class LanguageModelControl : BaseControl
    {
        private ListCollectionView _deviceCollectionView;
        private ListCollectionView _modelCollectionView;
        private ProcessType _processType;
        private DeviceModel _selectedDevice;
        private LanguageModel _selectedModel;
        private MemoryProfileModel _selectedMemoryMode;
        private ExecutionOptionsModel _selectedExecutionOptions;
        private DeviceModel _currentDevice;
        private LanguageModel _currentModel;
        private MemoryMode _currentMemoryMode;
        private ExecutionOptionsModel _currentExecutionOptions;
        private LanguageCheckpointModel _selectedCheckpoint;
        private List<LanguageCheckpointModel> _checkpointCollection;
        private DataType? _currentCheckpointType;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageModelControl"/> class.
        /// </summary>
        public LanguageModelControl()
        {
            MemoryModes =
            [
                new MemoryProfileModel{ MemoryMode = MemoryMode.Auto },
                new MemoryProfileModel{ MemoryMode = MemoryMode.Balanced },
                new MemoryProfileModel{ MemoryMode = MemoryMode.Low },
                new MemoryProfileModel{ MemoryMode = MemoryMode.Medium },
                new MemoryProfileModel{ MemoryMode = MemoryMode.High }
            ];
            LoadCommand = new AsyncRelayCommand(LoadAsync, CanLoad);
            UnloadCommand = new AsyncRelayCommand(UnloadAsync, CanUnload);
            InstallCommand = new AsyncRelayCommand<string>(OnInstall);
            DownloadCommand = new AsyncRelayCommand<string>(OnDownload);
            SettingsCommand = new AsyncRelayCommand<string>(OnSettings);
            ExecutionOptionsCommand = new AsyncRelayCommand(ExecutionOptionsAsync, CanExecutionOptions);
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(LanguageModelControl), new PropertyMetadata<LanguageModelControl>((c) => c.OnSettingsChanged()));
        public static readonly DependencyProperty IsPipelineLoadedProperty = DependencyProperty.Register(nameof(IsPipelineLoaded), typeof(bool), typeof(LanguageModelControl), new PropertyMetadata<LanguageModelControl>((c) => c.OnIsPipelineLoadedChanged()));
        public static readonly DependencyProperty IsSelectionValidProperty = DependencyProperty.Register(nameof(IsSelectionValid), typeof(bool), typeof(LanguageModelControl));
        public static readonly DependencyProperty DownloadServiceProperty = DependencyProperty.Register(nameof(DownloadService), typeof(IModelDownloadService), typeof(LanguageModelControl));
        public static readonly DependencyProperty NavigationServiceProperty = DependencyProperty.Register(nameof(NavigationService), typeof(NavigationService), typeof(LanguageModelControl));
        public event EventHandler<PipelineModel> SelectionChanged;
        public View ViewType { get; set; }
        public AsyncRelayCommand LoadCommand { get; }
        public AsyncRelayCommand UnloadCommand { get; }
        public AsyncRelayCommand<string> InstallCommand { get; }
        public AsyncRelayCommand<string> DownloadCommand { get; }
        public AsyncRelayCommand<string> SettingsCommand { get; }
        public AsyncRelayCommand ExecutionOptionsCommand { get; }
        public MemoryProfileModel[] MemoryModes { get; }

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public bool IsPipelineLoaded
        {
            get { return (bool)GetValue(IsPipelineLoadedProperty); }
            set { SetValue(IsPipelineLoadedProperty, value); }
        }

        public bool IsSelectionValid
        {
            get { return (bool)GetValue(IsSelectionValidProperty); }
            set { SetValue(IsSelectionValidProperty, value); }
        }

        public IModelDownloadService DownloadService
        {
            get { return (IModelDownloadService)GetValue(DownloadServiceProperty); }
            set { SetValue(DownloadServiceProperty, value); }
        }

        public NavigationService NavigationService
        {
            get { return (NavigationService)GetValue(NavigationServiceProperty); }
            set { SetValue(NavigationServiceProperty, value); }
        }

        public ProcessType ProcessType
        {
            get { return _processType; }
            set { SetProperty(ref _processType, value); }
        }

        public DeviceModel SelectedDevice
        {
            get { return _selectedDevice; }
            set { SetProperty(ref _selectedDevice, value); ValidateSelection(); }
        }

        public LanguageModel SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); ValidateSelection(); }
        }

        public List<LanguageCheckpointModel> CheckpointCollection
        {
            get { return _checkpointCollection; }
            set { SetProperty(ref _checkpointCollection, value); }
        }

        public LanguageCheckpointModel SelectedCheckpoint
        {
            get { return _selectedCheckpoint; }
            set
            {
                SetProperty(ref _selectedCheckpoint, value);
                RefreshMemoryProfile();
                ValidateSelection();
            }
        }

        public MemoryProfileModel SelectedMemoryMode
        {
            get { return _selectedMemoryMode; }
            set { SetProperty(ref _selectedMemoryMode, value); ValidateSelection(); }
        }

        public ExecutionOptionsModel SelectedExecutionOptions
        {
            get { return _selectedExecutionOptions; }
            set { SetProperty(ref _selectedExecutionOptions, value); ValidateSelection(); }
        }

        public ListCollectionView DeviceCollectionView
        {
            get { return _deviceCollectionView; }
            set { SetProperty(ref _deviceCollectionView, value); }
        }

        public ListCollectionView ModelCollectionView
        {
            get { return _modelCollectionView; }
            set { SetProperty(ref _modelCollectionView, value); }
        }

        private async Task LoadAsync()
        {
            var isReady = await DownloadService.PrepareLoadAsync(_selectedModel, _selectedCheckpoint?.Type, []);
            RefreshCheckpointSelection();
            if (!isReady)
                return;

            _currentDevice = SelectedDevice;
            _currentModel = SelectedModel;
            _currentCheckpointType = _currentModel.Checkpoint?.Type;
            _currentMemoryMode = _selectedMemoryMode.MemoryMode;
            _currentExecutionOptions = _selectedExecutionOptions;

            var pipeline = new PipelineModel
            {
                Device = _currentDevice,
                LanguageModel = _currentModel,
                CheckpointType = _currentCheckpointType,
                MemoryMode = _currentMemoryMode,
                ExecutionOptions = _currentExecutionOptions,
                ProcessType = _processType,
                ViewType = ViewType
            };

            SelectionChanged?.Invoke(this, pipeline);
            ValidateSelection();
        }


        private bool CanLoad()
        {
            return _selectedDevice != null && _selectedModel != null && !IsSelectionValid;
        }


        private Task UnloadAsync()
        {
            _currentModel = default;
            _currentCheckpointType = default;
            IsSelectionValid = false;
            _currentExecutionOptions = default;

            var pipeline = new PipelineModel
            {
                Device = _selectedDevice,
                MemoryMode = _selectedMemoryMode.MemoryMode,
                ProcessType = _processType,
                ViewType = ViewType
            };

            SelectionChanged?.Invoke(this, pipeline);
            Model_SelectionChanged(default, default);

            ValidateSelection();
            return Task.CompletedTask;
        }


        private bool CanUnload()
        {
            return _currentModel is not null;
        }


        private async Task ExecutionOptionsAsync()
        {
            var dialog = DialogService.GetDialog<ExecutionOptionsDialog>();
            if (await dialog.ShowDialogAsync(_selectedModel.Backend, _selectedExecutionOptions))
            {
                SelectedExecutionOptions = dialog.ExecutionOptions;
            }
        }


        private bool CanExecutionOptions()
        {
            return _selectedModel != null;
        }


        private Task OnIsPipelineLoadedChanged()
        {
            ValidateSelection();
            return Task.CompletedTask;
        }


        private void ValidateSelection()
        {
            var isModelValid = ModelCollectionView?.IsEmpty == false;
            var isCurrentValid = !HasCurrentChanged();
            IsSelectionValid = isCurrentValid && isModelValid && IsPipelineLoaded;
            LoadCommand.RaiseCanExecuteChanged();
        }


        private bool HasCurrentChanged()
        {
            return _currentDevice != SelectedDevice
                || _currentModel != SelectedModel
                || _currentCheckpointType != SelectedCheckpoint?.Type
                || _currentMemoryMode != _selectedMemoryMode?.MemoryMode
                || _currentExecutionOptions != SelectedExecutionOptions;
        }


        private Task OnSettingsChanged()
        {
            // Devices
            DeviceCollectionView = new ListCollectionView(Settings.Devices);
            DeviceCollectionView.Filter = (obj) =>
            {
                if (obj is not DeviceModel device)
                    return false;

                if (!Settings.Vendors.Contains(device.Vendor))
                    return false;

                return true;
            };

            // Base Models
            ModelCollectionView = new ListCollectionView(Settings.LanguageModels);
            ModelCollectionView.IsLiveSorting = true;
            ModelCollectionView.IsLiveFiltering = true;
            ModelCollectionView.SortDescriptions.Add(new SortDescription(nameof(LanguageModel.Status), ListSortDirection.Descending));
            ModelCollectionView.SortDescriptions.Add(new SortDescription(nameof(LanguageModel.Name), ListSortDirection.Ascending));
            ModelCollectionView.Filter = (obj) =>
            {
                if (obj is not LanguageModel viewModel)
                    return false;

                if (Settings.IsInstalledModelsOnly && viewModel.Status == ModelStatusType.Available)
                    return false;

                if (!viewModel.IsCompatible(_selectedDevice))
                    return false;

                if (!viewModel.ProcessTypes.Contains(_processType))
                    return false;

                if (!viewModel.ViewFilter.IsNullOrEmpty() && !viewModel.ViewFilter.Contains(ViewType))
                    return false;

                return true;
            };

            SelectedDevice = Settings.GetDefaultDevice();
            Device_SelectionChanged(default, default);

            Settings.PropertyChanged += (s, p) =>
            {
                if (p.PropertyName == nameof(Settings.Vendors))
                {
                    DeviceCollectionView.Refresh();
                    SelectedDevice = Settings.GetDefaultDevice();
                }
                else if (p.PropertyName == nameof(Settings.IsInstalledModelsOnly))
                {
                    Device_SelectionChanged(default, default);
                }
            };

            return Task.CompletedTask;
        }


        private void Device_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ModelCollectionView is not null)
            {
                ModelCollectionView.Refresh();
                SelectedModel = ModelCollectionView.Cast<LanguageModel>().FirstOrDefault(x => x == _currentModel)
                             ?? ModelCollectionView.Cast<LanguageModel>().FirstOrDefault(x => x.IsDefault && x.Status == ModelStatusType.Installed && x.ProcessTypes.Contains(_processType))
                             ?? ModelCollectionView.Cast<LanguageModel>().FirstOrDefault(x => x.Status == ModelStatusType.Installed)
                             ?? ModelCollectionView.Cast<LanguageModel>().FirstOrDefault(x => x.IsDefault && x.ProcessTypes.Contains(_processType))
                             ?? ModelCollectionView.Cast<LanguageModel>().FirstOrDefault();
            }

            RefreshMemoryProfile();
        }


        private void Model_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshCheckpointSelection();
            if (_selectedModel is null)
                return;

            RefreshMemoryProfile();
            SelectedMemoryMode = _selectedModel.UserMemoryMode is null
                ? MemoryModes.FirstOrDefault(x => x.MemoryMode == MemoryMode.Auto)
                : MemoryModes.FirstOrDefault(x => x.MemoryMode == _selectedModel.UserMemoryMode.Value);
            SelectedExecutionOptions = _selectedModel.UserExecutionOptions ?? new ExecutionOptionsModel();
        }


        /// <summary>
        /// Refreshes the checkpoint sets and the selected set from the selected model.
        /// </summary>
        private void RefreshCheckpointSelection()
        {
            CheckpointCollection = _selectedModel?.Checkpoints;
            SelectedCheckpoint = _selectedModel?.Checkpoint;
        }


        /// <summary>
        /// Handles the memory mode selection changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.Controls.SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void Memory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshMemoryProfile();
        }


        /// <summary>
        /// Refreshes the backend descriptions and the mode Auto resolves to for the selected model and device.
        /// </summary>
        private void RefreshMemoryProfile()
        {
            foreach (var memoryMode in MemoryModes)
                memoryMode.Description = memoryMode.MemoryMode.GetDescription(_selectedModel?.Backend);

            if (_selectedDevice is null || _selectedModel is null)
                return;

            MemoryModes[0].DetectedMode = Extensions.ResolveMemoryMode(_selectedCheckpoint?.MemorySize ?? _selectedModel.MemorySize, _selectedDevice.MemoryGB);
        }


        public void SetPipeline(PipelineModel pipeline)
        {
            RefreshSelection();
            if (pipeline == null)
                return;

            if (!ModelCollectionView.Contains(pipeline.LanguageModel))
                return;

            SelectedDevice = pipeline.Device;
            SelectedModel = pipeline.LanguageModel;
            RefreshCheckpointSelection();
            SelectedMemoryMode = MemoryModes.FirstOrDefault(x => x.MemoryMode == pipeline.MemoryMode);
            SelectedExecutionOptions = _selectedModel?.UserExecutionOptions ?? new ExecutionOptionsModel();
            ValidateSelection();
        }


        /// <summary>
        /// Selects the model if it is available for the current view and device.
        /// </summary>
        /// <param name="model">The model.</param>
        public void SelectModel(LanguageModel model)
        {
            RefreshSelection();
            if (model == null || !ModelCollectionView.Contains(model))
                return;

            SelectedModel = model;
            ValidateSelection();
        }


        private void RefreshSelection()
        {
            if (ModelCollectionView != null)
            {
                ModelCollectionView.Refresh();
                if (!ModelCollectionView.IsEmpty && _selectedModel == null)
                    ModelCollectionView.MoveCurrentToFirst();

                RefreshCheckpointSelection();
            }
        }


        private async Task OnInstall(string sender)
        {
            if (sender.Equals("Language"))
            {
                var dialog = DialogService.GetDialog<LanguageModelWizardDialog>();
                if (await dialog.ShowDialogAsync())
                {
                    SelectedModel = dialog.SelectedTemplate;
                }
            }
        }


        private async Task OnDownload(string sender)
        {
            if (sender.Equals("Language"))
            {
                await NavigationService.NavigateAsync((int)View.Models, new ModelViewOpenArgs(ViewType, ModelCategoryType.LLM));
            }
        }


        private async Task OnSettings(string sender)
        {
            if (sender.Equals("Language"))
            {
                await NavigationService.NavigateAsync((int)View.SettingsLanguage);
            }
        }
    }
}
