using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private QualityProfileModel _selectedQualityMode;
        private DeviceModel _currentDevice;
        private LanguageModel _currentModel;
        private QualityMode _currentQualityMode;


        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageModelControl"/> class.
        /// </summary>
        public LanguageModelControl()
        {
            QualityModes = [];
            LoadCommand = new AsyncRelayCommand(LoadAsync, CanLoad);
            UnloadCommand = new AsyncRelayCommand(UnloadAsync, CanUnload);
            InstallCommand = new AsyncRelayCommand<string>(OnInstall);
            DownloadCommand = new AsyncRelayCommand<string>(OnDownload);
            SettingsCommand = new AsyncRelayCommand<string>(OnSettings);
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
        public ObservableCollection<QualityProfileModel> QualityModes { get; }

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

        public QualityProfileModel SelectedQualityMode
        {
            get { return _selectedQualityMode; }
            set { SetProperty(ref _selectedQualityMode, value); ValidateSelection(); }
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
            if (await IsDownloadingAsync())
                return;

            _currentDevice = SelectedDevice;
            _currentModel = SelectedModel;
            _currentQualityMode = _selectedQualityMode.DetectedMode;

            var pipeline = new PipelineModel
            {
                Device = _currentDevice,
                LanguageModel = _currentModel,
                MemoryMode = _selectedQualityMode.MemoryMode,
                QualityMode = _selectedQualityMode.DetectedMode,
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
            IsSelectionValid = false;

            var pipeline = new PipelineModel
            {
                Device = _selectedDevice,
                MemoryMode = _selectedQualityMode.MemoryMode,
                QualityMode = _selectedQualityMode.DetectedMode,
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
                || _currentQualityMode != _selectedQualityMode?.DetectedMode;
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

                if (viewModel.Status == ModelStatusType.Available)
                    return false;

                if (_selectedDevice is null)
                    return false;

                if (!_selectedDevice.SupportedBackends.Contains(viewModel.Backend))
                    return false;

                if (!viewModel.ProcessTypes.Contains(_processType))
                    return false;

                if (!viewModel.Vendor.IsNullOrEmpty() && !viewModel.Vendor.Contains(_selectedDevice.Vendor))
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
            };

            return Task.CompletedTask;
        }


        private void Device_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SetDeviceDataTypes();
            if (ModelCollectionView is not null)
            {
                ModelCollectionView.Refresh();
                SelectedModel = ModelCollectionView.Cast<LanguageModel>().FirstOrDefault(x => x == _currentModel)
                             ?? ModelCollectionView.Cast<LanguageModel>().FirstOrDefault();
            }

            RefreshMemoryProfile();
        }


        private void Model_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_selectedModel is null)
                return;

            RefreshMemoryProfile();
            SelectedQualityMode = QualityModes.FirstOrDefault(x => x.QualityMode == _selectedModel.UserQualityMode);
        }


        private void SetDeviceDataTypes()
        {
            if (_selectedDevice is null)
                return;

            QualityModes.Clear();
            foreach (var qualityProfile in CreateQualityProfiles(_selectedDevice.QualityModes))
            {
                QualityModes.Add(qualityProfile);
            }

            if (_selectedQualityMode == null || !_selectedDevice.QualityModes.Contains(_selectedQualityMode.DetectedMode))
                SelectedQualityMode = QualityModes.FirstOrDefault();
        }


        private void RefreshMemoryProfile()
        {
            if (_selectedDevice is null || _selectedModel is null)
                return;

            var deviceMemory = _selectedDevice.MemoryGB;
            var autoIndex = GetQualityProfileIndex(_selectedModel.MemoryProfile, _selectedDevice.QualityModes, deviceMemory);
            var autoProfile = _selectedModel.MemoryProfile.ElementAtOrDefault(autoIndex);
            if (autoProfile is null)
                return;

            // Auto
            QualityModes[0].MemoryGB = autoProfile.MemoryModes.ElementAtOrDefault(0);
            QualityModes[0].DetectedMode = Enum.GetValues<QualityMode>()[autoIndex];
            foreach (var profile in _selectedModel.MemoryProfile)
            {
                var memoryMode = QualityModes.FirstOrDefault(x => x.QualityMode == profile.QualityMode);
                if (memoryMode == null)
                    continue;

                memoryMode.MemoryGB = profile.MemoryModes.ElementAtOrDefault(0);
            }
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
            SelectedQualityMode = pipeline.MemoryMode == MemoryMode.Auto
                ? QualityModes.FirstOrDefault(x => x.MemoryMode == MemoryMode.Auto)
                : QualityModes.FirstOrDefault(x => x.QualityMode == pipeline.QualityMode);
            ValidateSelection();
        }


        private void RefreshSelection()
        {
            if (ModelCollectionView != null)
            {
                ModelCollectionView.Refresh();
                if (!ModelCollectionView.IsEmpty && _selectedModel == null)
                    ModelCollectionView.MoveCurrentToFirst();
            }
        }


        private async Task<bool> IsDownloadingAsync()
        {
            var status = GetPipelineStatus();
            if (status.Contains(ModelStatusType.Downloading) || status.Contains(ModelStatusType.DownloadQueue) || status.Contains(ModelStatusType.DownloadFailed))
            {
                await DialogService.ShowMessageAsync("Model Downloading", "This model is downloading or queued for download", TensorStack.WPF.Dialogs.MessageDialogType.Ok, TensorStack.WPF.Dialogs.MessageBoxIconType.Info, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info);
                return true;
            }
            else if (status.Contains(ModelStatusType.Available) || status.Contains(ModelStatusType.Unknown))
            {
                var queueDownload = await DialogService.ShowMessageAsync("Queue Download", "Would you like to queue this model for download?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info);
                if (queueDownload)
                {
                    // Base Model
                    if (_selectedModel.Status != ModelStatusType.Installed)
                    {
                        if (!await DownloadService.QueueAsync(_selectedModel))
                            return true;
                    }
                    await NavigationService.NavigateAsync((int)View.Models);
                }
                return true;
            }
            return false;
        }


        private List<ModelStatusType> GetPipelineStatus()
        {
            return [_selectedModel.Status];
        }


        private static IEnumerable<QualityProfileModel> CreateQualityProfiles(QualityMode[] qualityModes)
        {
            yield return new QualityProfileModel
            {
                QualityMode = default,
                MemoryMode = MemoryMode.Auto
            };
            if (qualityModes.Contains(QualityMode.Draft))
                yield return new QualityProfileModel
                {
                    QualityMode = QualityMode.Draft,
                    DetectedMode = QualityMode.Draft,
                    MemoryMode = MemoryMode.High
                };
            if (qualityModes.Contains(QualityMode.Standard))
                yield return new QualityProfileModel
                {
                    QualityMode = QualityMode.Standard,
                    DetectedMode = QualityMode.Standard,
                    MemoryMode = MemoryMode.High
                };
            if (qualityModes.Contains(QualityMode.Production))
                yield return new QualityProfileModel
                {
                    QualityMode = QualityMode.Production,
                    DetectedMode = QualityMode.Production,
                    MemoryMode = MemoryMode.High
                };
        }


        private static int GetQualityProfileIndex(MemoryProfile[] memoryProfile, QualityMode[] qualityModes, int deviceMemory)
        {
            int bestIndex = -1;
            int bestValue = int.MinValue;
            for (int i = 0; i < memoryProfile.Length; i++)
            {
                var profile = memoryProfile[i];
                if (!qualityModes.Contains(profile.QualityMode))
                    continue;

                int value = profile.MemoryModes[0];
                if (value <= deviceMemory && value >= bestValue)
                {
                    bestValue = value;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                var defaultProfile = memoryProfile.FirstOrDefault(x => x.QualityMode == qualityModes.FirstOrDefault());
                if (defaultProfile != null)
                {
                    bestIndex = Array.IndexOf(memoryProfile, defaultProfile);
                }
            }

            return bestIndex;
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
                await NavigationService.NavigateAsync((int)View.Language);
            }
        }
    }
}
