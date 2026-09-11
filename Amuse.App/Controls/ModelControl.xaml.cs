using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.App.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for ModelControl.xaml
    /// </summary>
    public partial class ModelControl : BaseControl
    {
        private ListCollectionView _deviceCollectionView;
        private ListCollectionView _extractCollectionView;
        private ListCollectionView _upscaleCollectionView;

        private DeviceModel _selectedDevice;
        private ExtractModel _selectedExtractor;
        private UpscaleModel _selectedUpscaler;

        private bool _isUpscalerEnabled;
        private bool _isExtractorEnabled;

        private DeviceModel _currentDevice;
        private ExtractModel _currentExtractor;
        private UpscaleModel _currentUpscaler;


        /// <summary>
        /// Initializes a new instance of the <see cref="ModelControl"/> class.
        /// </summary>
        public ModelControl()
        {
            LoadCommand = new AsyncRelayCommand(LoadAsync, CanLoad);
            UnloadCommand = new AsyncRelayCommand(UnloadAsync, CanUnload);
            InstallCommand = new AsyncRelayCommand<string>(OnInstall);
            DownloadCommand = new AsyncRelayCommand<string>(OnDownload);
            SettingsCommand = new AsyncRelayCommand<string>(OnSettings);
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(ModelControl), new PropertyMetadata<ModelControl>((c) => c.OnSettingsChanged()));
        public static readonly DependencyProperty IsPipelineLoadedProperty = DependencyProperty.Register(nameof(IsPipelineLoaded), typeof(bool), typeof(ModelControl), new PropertyMetadata<ModelControl>((c) => c.OnIsPipelineLoadedChanged()));
        public static readonly DependencyProperty IsSelectionValidProperty = DependencyProperty.Register(nameof(IsSelectionValid), typeof(bool), typeof(ModelControl));
        public static readonly DependencyProperty DownloadServiceProperty = DependencyProperty.Register(nameof(DownloadService), typeof(IModelDownloadService), typeof(ModelControl));
        public static readonly DependencyProperty NavigationServiceProperty = DependencyProperty.Register(nameof(NavigationService), typeof(NavigationService), typeof(ModelControl));

        public event EventHandler<PipelineModel> SelectionChanged;
        public AsyncRelayCommand LoadCommand { get; }
        public AsyncRelayCommand UnloadCommand { get; }
        public AsyncRelayCommand<string> InstallCommand { get; }
        public AsyncRelayCommand<string> DownloadCommand { get; }
        public AsyncRelayCommand<string> SettingsCommand { get; }
        public View ViewType { get; set; }

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

        public DeviceModel SelectedDevice
        {
            get { return _selectedDevice; }
            set { SetProperty(ref _selectedDevice, value); ValidateSelection(); }
        }

        public ExtractModel SelectedExtractor
        {
            get { return _selectedExtractor; }
            set { SetProperty(ref _selectedExtractor, value); ValidateSelection(); }
        }

        public UpscaleModel SelectedUpscaler
        {
            get { return _selectedUpscaler; }
            set { SetProperty(ref _selectedUpscaler, value); ValidateSelection(); }
        }

        public ListCollectionView DeviceCollectionView
        {
            get { return _deviceCollectionView; }
            set { SetProperty(ref _deviceCollectionView, value); }
        }

        public ListCollectionView ExtractCollectionView
        {
            get { return _extractCollectionView; }
            set { SetProperty(ref _extractCollectionView, value); }
        }

        public ListCollectionView UpscaleCollectionView
        {
            get { return _upscaleCollectionView; }
            set { SetProperty(ref _upscaleCollectionView, value); }
        }

        public bool IsExtractorEnabled
        {
            get { return _isExtractorEnabled; }
            set { SetProperty(ref _isExtractorEnabled, value); }
        }

        public bool IsUpscalerEnabled
        {
            get { return _isUpscalerEnabled; }
            set { SetProperty(ref _isUpscalerEnabled, value); }
        }

        private async Task LoadAsync()
        {
            if (!await IsAccessGrantedAsync())
                return;

            if (await IsDownloadingAsync())
                return;

            _currentDevice = SelectedDevice;
            _currentExtractor = SelectedExtractor;
            _currentUpscaler = SelectedUpscaler;

            var pipeline = new PipelineModel
            {
                Device = _currentDevice,
                ExtractModel = _isExtractorEnabled ? _currentExtractor : default,
                UpscaleModel = _isUpscalerEnabled ? _currentUpscaler : default,
                ViewType = ViewType
            };

            ValidateSelection();
            SelectionChanged?.Invoke(this, pipeline);
        }


        private bool CanLoad()
        {
            return _selectedDevice != null && !IsSelectionValid && (_selectedUpscaler != null || _selectedExtractor != null); 
        }


        private Task UnloadAsync()
        {
            _currentExtractor = default;
            _currentUpscaler = default;
            SelectionChanged?.Invoke(this, default);
            ValidateSelection();
            return Task.CompletedTask;
        }


        private bool CanUnload()
        {
            return _currentExtractor is not null
                || _currentUpscaler is not null;
        }


        private bool HasCurrentChanged()
        {
            return _currentDevice != SelectedDevice
                || (IsExtractorEnabled && _currentExtractor != SelectedExtractor)
                || (IsUpscalerEnabled && _currentUpscaler != SelectedUpscaler);
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


            // Extractor Models
            ExtractCollectionView = new ListCollectionView(Settings.ExtractModels);
            ExtractCollectionView.Filter = (obj) =>
            {
                if (obj is not ExtractModel viewModel)
                    return false;

                if (viewModel.Status == ModelStatusType.Available)
                    return false;

                if (_selectedDevice is null)
                    return false;

                return true;
            };


            //Upscale models
            UpscaleCollectionView = new ListCollectionView(Settings.UpscaleModels);
            UpscaleCollectionView.Filter = (obj) =>
            {
                if (obj is not UpscaleModel viewModel)
                    return false;

                if (viewModel.Status == ModelStatusType.Available)
                    return false;

                if (_selectedDevice is null)
                    return false;

                return true;
            };


            SelectedDevice = Settings.GetDefaultDevice();
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
            if (ExtractCollectionView is not null)
            {
                ExtractCollectionView.Refresh();
                SelectedExtractor = ExtractCollectionView.Cast<ExtractModel>().FirstOrDefault(x => x == _currentExtractor)
                                 ?? ExtractCollectionView.Cast<ExtractModel>().OrderByDescending(x => x.IsDefault).FirstOrDefault();
            }

            if (UpscaleCollectionView is not null)
            {
                UpscaleCollectionView.Refresh();
                SelectedUpscaler = UpscaleCollectionView.Cast<UpscaleModel>().FirstOrDefault(x => x == _currentUpscaler)
                                ?? UpscaleCollectionView.Cast<UpscaleModel>().OrderByDescending(x => x.IsDefault).FirstOrDefault();
            }
        }


        private Task OnIsPipelineLoadedChanged()
        {
            ValidateSelection();
            return Task.CompletedTask;
        }


        private void ValidateSelection()
        {
            var isExtractValid = !IsExtractorEnabled || ExtractCollectionView?.IsEmpty == false;
            var isUpscaleValid = !IsUpscalerEnabled || UpscaleCollectionView?.IsEmpty == false;
            var isCurrentValid = !HasCurrentChanged();
            IsSelectionValid = isCurrentValid && isExtractValid && isUpscaleValid && IsPipelineLoaded;
            LoadCommand.RaiseCanExecuteChanged();
        }


        public void SetPipeline(PipelineModel pipeline)
        {
            RefreshSelection();
            if (pipeline == null)
                return;

            SelectedDevice = pipeline.Device;
            if (IsUpscalerEnabled && pipeline.UpscaleModel is not null && _upscaleCollectionView.Contains(pipeline.UpscaleModel))
                SelectedUpscaler = pipeline.UpscaleModel;

            if (IsExtractorEnabled && pipeline.ExtractModel is not null && _extractCollectionView.Contains(pipeline.ExtractModel))
                SelectedExtractor = pipeline.ExtractModel;

            ValidateSelection();
        }


        private void RefreshSelection()
        {
            if (ExtractCollectionView != null)
            {
                ExtractCollectionView.Refresh();
                if (!ExtractCollectionView.IsEmpty && _selectedExtractor == null && _isExtractorEnabled)
                    ExtractCollectionView.MoveCurrentToFirst();
            }

            if (UpscaleCollectionView != null)
            {
                UpscaleCollectionView.Refresh();
                if (!UpscaleCollectionView.IsEmpty && _selectedExtractor == null && _isUpscalerEnabled)
                    UpscaleCollectionView.MoveCurrentToFirst();
            }
        }


        private async Task<bool> IsDownloadingAsync()
        {
            var status = GetModelStatus();
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
                    // Extract
                    if (_isExtractorEnabled && _selectedExtractor != null && _selectedExtractor.Status != ModelStatusType.Installed)
                    {
                        await DownloadService.QueueAsync(_selectedExtractor);
                    }

                    // Upscale
                    if (_isUpscalerEnabled && _selectedUpscaler != null && _selectedUpscaler.Status != ModelStatusType.Installed)
                    {
                        await DownloadService.QueueAsync(_selectedUpscaler);
                    }
                    await NavigationService.NavigateAsync((int)View.Models);
                }
                return true;
            }
            return false;
        }


        private List<ModelStatusType> GetModelStatus()
        {
            var statuses = new List<ModelStatusType>();
            if (_isExtractorEnabled && _selectedExtractor != null)
            {
                statuses.Add(_selectedExtractor.Status);
            }

            if (_isUpscalerEnabled && _selectedUpscaler != null)
            {
                statuses.Add(_selectedUpscaler.Status);
            }

            return statuses;
        }


        private Task<bool> IsAccessGrantedAsync()
        {
            return Task.FromResult(true);
        }


        private async Task OnInstall(string sender)
        {
            if (sender.Equals("Extract"))
            {
                var dialog = DialogService.GetDialog<ExtractModelDialog>();
                if (await dialog.AddAsync())
                {
                    SelectedExtractor = dialog.ExtractModel;
                }
            }
            else if (sender.Equals("Upscale"))
            {
                var dialog = DialogService.GetDialog<UpscaleModelDialog>();
                if (await dialog.AddAsync())
                {
                    SelectedUpscaler = dialog.UpscaleModel;
                }
            }
        }


        private async Task OnDownload(string sender)
        {
            if (sender.Equals("Extract"))
            {
                await NavigationService.NavigateAsync((int)View.Models, new ModelViewOpenArgs(ViewType, ModelCategoryType.Extract));
            }
            else if (sender.Equals("Upscale"))
            {
                await NavigationService.NavigateAsync((int)View.Models, new ModelViewOpenArgs(ViewType, ModelCategoryType.Upscale));
            }
        }


        private async Task OnSettings(string sender)
        {
            if (sender.Equals("Extract"))
            {
                await NavigationService.NavigateAsync((int)View.Extract);
            }
            else if (sender.Equals("Upscale"))
            {
                await NavigationService.NavigateAsync((int)View.Upscale);
            }
        }
    }
}
