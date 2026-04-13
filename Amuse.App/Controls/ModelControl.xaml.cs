using Amuse.App.Common;
using Amuse.App.Views;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

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
        private ListCollectionView _audioCollectionView;

        private DeviceModel _selectedDevice;
        private ExtractModel _selectedExtractor;
        private UpscaleModel _selectedUpscaler;
        private AudioModel _selectedAudioModel;

        private bool _isUpscalerEnabled;
        private bool _isExtractorEnabled;
        private bool _isAudioEnabled;

        private DeviceModel _currentDevice;
        private ExtractModel _currentExtractor;
        private UpscaleModel _currentUpscaler;
        private AudioModel _currentAudioModel;


        /// <summary>
        /// Initializes a new instance of the <see cref="ModelControl"/> class.
        /// </summary>
        public ModelControl()
        {
            LoadCommand = new AsyncRelayCommand(LoadAsync, CanLoad);
            UnloadCommand = new AsyncRelayCommand(UnloadAsync, CanUnload);
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(ModelControl), new PropertyMetadata<ModelControl>((c) => c.OnSettingsChanged()));
        public static readonly DependencyProperty IsPipelineLoadedProperty = DependencyProperty.Register(nameof(IsPipelineLoaded), typeof(bool), typeof(ModelControl), new PropertyMetadata<ModelControl>((c) => c.OnIsPipelineLoadedChanged()));
        public static readonly DependencyProperty IsSelectionValidProperty = DependencyProperty.Register(nameof(IsSelectionValid), typeof(bool), typeof(ModelControl));

        public event EventHandler<PipelineModel> SelectionChanged;
        public AsyncRelayCommand LoadCommand { get; }
        public AsyncRelayCommand UnloadCommand { get; }
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

        public AudioModel SelectedAudioModel
        {
            get { return _selectedAudioModel; }
            set { SetProperty(ref _selectedAudioModel, value); ValidateSelection(); }
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

        public ListCollectionView AudioCollectionView
        {
            get { return _audioCollectionView; }
            set { SetProperty(ref _audioCollectionView, value); }
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

        public bool IsAudioEnabled
        {
            get { return _isAudioEnabled; }
            set { SetProperty(ref _isAudioEnabled, value); }
        }


        private Task LoadAsync()
        {
            _currentDevice = SelectedDevice;
            _currentExtractor = SelectedExtractor;
            _currentUpscaler = SelectedUpscaler;
            _currentAudioModel = SelectedAudioModel;

            var pipeline = new PipelineModel
            {
                Device = _currentDevice,
                ExtractModel = _isExtractorEnabled ? _currentExtractor : default,
                UpscaleModel = _isUpscalerEnabled ? _currentUpscaler : default,
                AudioModel = _isAudioEnabled ? _currentAudioModel : default,
            };

            ValidateSelection();
            SelectionChanged?.Invoke(this, pipeline);
            return Task.CompletedTask;
        }


        private bool CanLoad()
        {
            return !IsSelectionValid;
        }


        private Task UnloadAsync()
        {
            _currentExtractor = default;
            _currentUpscaler = default;
            _currentAudioModel = default;
            SelectionChanged?.Invoke(this, default);
            ValidateSelection();
            return Task.CompletedTask;
        }


        private bool CanUnload()
        {
            return _currentExtractor is not null
                || _currentUpscaler is not null
                || _currentAudioModel is not null;
        }


        private bool HasCurrentChanged()
        {
            return _currentDevice != SelectedDevice
                || (IsExtractorEnabled && _currentExtractor != SelectedExtractor)
                || (IsUpscalerEnabled && _currentUpscaler != SelectedUpscaler)
                || (IsAudioEnabled && _currentAudioModel != SelectedAudioModel);
        }


        private Task OnSettingsChanged()
        {
            // Devices
            DeviceCollectionView = new ListCollectionView(Settings.Devices);
            DeviceCollectionView.Filter = (obj) =>
            {
                if (obj is not DeviceModel device)
                    return false;

                return true;
            };


            // Extractor Models
            ExtractCollectionView = new ListCollectionView(Settings.ExtractModels);
            ExtractCollectionView.Filter = (obj) =>
            {
                if (obj is not ExtractModel viewModel)
                    return false;

                if (_selectedDevice is null)
                    return false;

                return true;
            };


            //Upscale models
            UpscaleCollectionView = new ListCollectionView(Settings.UpscaleModels);
            UpscaleCollectionView.Filter = (obj) =>
            {
                if (obj is not UpscaleModel model)
                    return false;

                if (_selectedDevice is null)
                    return false;

                return true;
            };


            //Audio models
            AudioCollectionView = new ListCollectionView(Settings.AudioModels);
            AudioCollectionView.Filter = (obj) =>
            {
                if (obj is not AudioModel model)
                    return false;

                if (_selectedDevice is null)
                    return false;

                if (ViewType == View.TextToAudio)
                    return model.Type == AudioModelType.Supertonic;

                if (ViewType == View.AudioToText)
                    return model.Type == AudioModelType.Whisper;

                return false;
            };

            SelectedDevice = Settings.GetDefaultDevice();
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

            if (AudioCollectionView is not null)
            {
                AudioCollectionView.Refresh();
                SelectedAudioModel = AudioCollectionView.Cast<AudioModel>().FirstOrDefault(x => x == _currentAudioModel)
                                  ?? AudioCollectionView.Cast<AudioModel>().OrderByDescending(x => x.IsDefault).FirstOrDefault();
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
            var isAudioValid = !IsAudioEnabled || AudioCollectionView?.IsEmpty == false;
            var isCurrentValid = !HasCurrentChanged();
            IsSelectionValid = isCurrentValid && isExtractValid && isAudioValid && IsPipelineLoaded;
            LoadCommand.RaiseCanExecuteChanged();
        }


        public void SetPipeline(PipelineModel pipeline)
        {
            if (pipeline == null)
                return;

            SelectedDevice = pipeline.Device;
            if (IsUpscalerEnabled && pipeline.UpscaleModel is not null && _upscaleCollectionView.Contains(pipeline.UpscaleModel))
                SelectedUpscaler = pipeline.UpscaleModel;

            if (IsExtractorEnabled && pipeline.ExtractModel is not null && _extractCollectionView.Contains(pipeline.ExtractModel))
                SelectedExtractor = pipeline.ExtractModel;

            if (IsAudioEnabled && pipeline.AudioModel is not null && _audioCollectionView.Contains(pipeline.AudioModel))
                SelectedAudioModel = pipeline.AudioModel;

            ValidateSelection();
        }
    }
}
