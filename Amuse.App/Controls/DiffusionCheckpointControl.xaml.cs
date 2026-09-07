using Amuse.App.Common;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for DiffusionCheckpointControl.xaml
    /// </summary>
    public partial class DiffusionCheckpointControl : BaseControl
    {
        private int _selectedIndex;
        private CheckpointType[] _checkpointTypes;
        private CheckpointType[] _textEncodedCheckpointTypes;
        private bool _isOnlineCheckpointsEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffusionCheckpointControl"/> class.
        /// </summary>
        public DiffusionCheckpointControl()
        {
            Components = new ObservableCollection<CheckpointComponent>();
            CheckpointTypes = [CheckpointType.LocalFile, CheckpointType.LocalFolder, CheckpointType.Component, CheckpointType.OnlineFile, CheckpointType.OnlineFolder];
            ComputeCheckpointTypes = [CheckpointType.LocalFolder, CheckpointType.OnlineFolder];
            TextEncodedCheckpointTypes = [CheckpointType.LocalFolder, CheckpointType.OnlineFolder, CheckpointType.Component];
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(DiffusionCheckpointControl));
        public static readonly DependencyProperty CheckpointProperty = DependencyProperty.Register(nameof(Checkpoint), typeof(DiffusionCheckpointModel), typeof(DiffusionCheckpointControl), new PropertyMetadata<DiffusionCheckpointControl, DiffusionCheckpointModel>((c, o, n) => c.OnCheckpointChanged(o, n)));
        public static readonly DependencyProperty BackendProperty = DependencyProperty.Register(nameof(Backend), typeof(BackendType), typeof(DiffusionCheckpointControl), new PropertyMetadata(BackendType.OnnxRuntime, OnBackendPropertyChanged));
        public ObservableCollection<CheckpointComponent> Components { get; }
        public CheckpointType[] ComputeCheckpointTypes { get; }

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public DiffusionCheckpointModel Checkpoint
        {
            get { return (DiffusionCheckpointModel)GetValue(CheckpointProperty); }
            set { SetValue(CheckpointProperty, value); }
        }

        public BackendType Backend
        {
            get { return (BackendType)GetValue(BackendProperty); }
            set { SetValue(BackendProperty, value); }
        }

        public CheckpointType[] CheckpointTypes
        {
            get { return _checkpointTypes; }
            set { SetProperty(ref _checkpointTypes, value); }
        }

        public CheckpointType[] TextEncodedCheckpointTypes
        {
            get { return _textEncodedCheckpointTypes; }
            set { SetProperty(ref _textEncodedCheckpointTypes, value); }
        }

        public bool IsOnlineCheckpointsEnabled
        {
            get { return _isOnlineCheckpointsEnabled; }
            set { SetProperty(ref _isOnlineCheckpointsEnabled, value); OnBackendChanged(); }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetProperty(ref _selectedIndex, value); }
        }


        private Task OnCheckpointChanged(DiffusionCheckpointModel previous, DiffusionCheckpointModel checkpoint)
        {
            Components.Clear();
            if (checkpoint != null)
            {
                foreach (var component in checkpoint.GetComponents())
                {
                    Components.Add(component);
                }

                if (checkpoint.Compute != null)
                    SelectedIndex = 0;
                else if (checkpoint.Unet != null)
                    SelectedIndex = 4;
                else if (checkpoint.Transformer != null)
                    SelectedIndex = 5;
                else if (checkpoint.TextEncoder != null)
                    SelectedIndex = 1;
            }
            return Task.CompletedTask;
        }


        private void OnBackendChanged()
        {
            if (Backend == BackendType.HuggingFace)
            {
                TextEncodedCheckpointTypes = IsOnlineCheckpointsEnabled
                    ? [CheckpointType.LocalFolder, CheckpointType.OnlineFolder, CheckpointType.Component]
                    : [CheckpointType.LocalFolder, CheckpointType.Component];
                CheckpointTypes = IsOnlineCheckpointsEnabled
                   ? [CheckpointType.LocalFile, CheckpointType.LocalFolder, CheckpointType.Component, CheckpointType.OnlineFile, CheckpointType.OnlineFolder]
                   : [CheckpointType.LocalFile, CheckpointType.LocalFolder, CheckpointType.Component];
            }
            if (Backend == BackendType.StableDiffusionCpp)
            {
                TextEncodedCheckpointTypes = IsOnlineCheckpointsEnabled
                    ? [CheckpointType.LocalFile, CheckpointType.OnlineFile, CheckpointType.OnlineFolder, CheckpointType.Component]
                    : [CheckpointType.LocalFile, CheckpointType.Component];
                CheckpointTypes = IsOnlineCheckpointsEnabled
                   ? [CheckpointType.LocalFile, CheckpointType.Component, CheckpointType.OnlineFile, CheckpointType.OnlineFolder]
                   : [CheckpointType.LocalFile, CheckpointType.Component];
            }
        }


        private static void OnBackendPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DiffusionCheckpointControl control)
            {
                control.OnBackendChanged();
            }
        }

    }
}