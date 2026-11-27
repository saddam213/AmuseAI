using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for CheckpointControl.xaml
    /// </summary>
    public partial class CheckpointControl : BaseControl
    {
        private static readonly string[] _modelExtensions = [".safetensors", ".gguf", ".onnx"];

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointControl"/> class.
        /// </summary>
        public CheckpointControl()
        {
            MainFiles = new ObservableCollection<string>();
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(CheckpointControl));
        public static readonly DependencyProperty CheckpointProperty = DependencyProperty.Register(nameof(Checkpoint), typeof(CheckpointComponent), typeof(CheckpointControl), new PropertyMetadata(OnCheckpointPropertyChanged));
        public static readonly DependencyProperty IsDiffusionCheckpointProperty = DependencyProperty.Register(nameof(IsDiffusionCheckpoint), typeof(bool), typeof(CheckpointControl));
        public static readonly DependencyProperty SupportedTypesProperty = DependencyProperty.Register(nameof(SupportedTypes), typeof(CheckpointType[]), typeof(CheckpointControl), new PropertyMetadata(Enum.GetValues<CheckpointType>()));
        public static readonly DependencyProperty FileFilterProperty = DependencyProperty.Register(nameof(FileFilter), typeof(string), typeof(CheckpointControl), new PropertyMetadata($"{AppDefault.ModelFiles}|*.safetensors;*.gguf;"));

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public CheckpointComponent Checkpoint
        {
            get { return (CheckpointComponent)GetValue(CheckpointProperty); }
            set { SetValue(CheckpointProperty, value); }
        }

        public bool IsDiffusionCheckpoint
        {
            get { return (bool)GetValue(IsDiffusionCheckpointProperty); }
            set { SetValue(IsDiffusionCheckpointProperty, value); }
        }

        public CheckpointType[] SupportedTypes
        {
            get { return (CheckpointType[])GetValue(SupportedTypesProperty); }
            set { SetValue(SupportedTypesProperty, value); }
        }

        /// <summary>
        /// Gets or sets the local file picker filter.
        /// </summary>
        public string FileFilter
        {
            get { return (string)GetValue(FileFilterProperty); }
            set { SetValue(FileFilterProperty, value); }
        }

        public ObservableCollection<string> MainFiles { get; }

        public IReadOnlyList<string> ComponentNames { get; } =
        [
            "text_encoder",
            "text_encoder_2",
            "text_encoder_3",
            "unet",
            "transformer",
            "transformer_2",
            "vae",
            "audio_vae",
            "vocoder",
            "connectors",
            "latent_upsampler",
            "latent_upsampler_temporal",
            "condition_encoder",
            "audio_tokenizer",
            "audio_token_detokenizer",
        ];


        /// <summary>
        /// Called when the checkpoint changes, the main files follow the checkpoint shown.
        /// </summary>
        /// <param name="d">The control.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnCheckpointPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CheckpointControl control)
                return;

            if (e.OldValue is CheckpointComponent previous)
                previous.PropertyChanged -= control.Checkpoint_PropertyChanged;

            if (e.NewValue is CheckpointComponent checkpoint)
                checkpoint.PropertyChanged += control.Checkpoint_PropertyChanged;

            control.UpdateMainFiles();
        }


        /// <summary>
        /// Handles a change of the checkpoint source, the main files are read again.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Checkpoint_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckpointComponent.Type) || e.PropertyName == nameof(CheckpointComponent.Path) || e.PropertyName == nameof(CheckpointComponent.DownloadFiles))
                UpdateMainFiles();
        }


        /// <summary>
        /// Fills the main files a folder checkpoint can choose from, the files of a local folder or the files of the download links.
        /// The main file set is kept, a folder checkpoint with none gets the first model file.
        /// </summary>
        private void UpdateMainFiles()
        {
            var checkpoint = Checkpoint;
            if (checkpoint == null)
            {
                MainFiles.Clear();
                return;
            }

            var mainFile = checkpoint.MainFile;
            var mainFiles = GetMainFiles(checkpoint);
            if (!string.IsNullOrEmpty(mainFile) && !mainFiles.Contains(mainFile))
                mainFiles.Insert(0, mainFile);

            MainFiles.Clear();
            foreach (var file in mainFiles)
                MainFiles.Add(file);

            var isFolder = checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.LocalFolder;
            if (isFolder && string.IsNullOrEmpty(mainFile))
                mainFile = mainFiles.FirstOrDefault(x => DownloadLink.IsSupportedFile(x, _modelExtensions)) ?? mainFiles.FirstOrDefault();

            checkpoint.MainFile = mainFile;
        }


        /// <summary>
        /// Gets the file names of a folder checkpoint, none when it is not a folder or the folder can't be read.
        /// </summary>
        /// <param name="checkpoint">The checkpoint.</param>
        private static List<string> GetMainFiles(CheckpointComponent checkpoint)
        {
            try
            {
                if (checkpoint.Type == CheckpointType.OnlineFolder)
                    return [.. (checkpoint.DownloadFiles ?? []).Select(DownloadLink.GetLocalFileName).Where(x => !string.IsNullOrEmpty(x)).Distinct()];

                if (checkpoint.Type == CheckpointType.LocalFolder && Directory.Exists(checkpoint.Path))
                    return [.. Directory.EnumerateFiles(checkpoint.Path).Select(Path.GetFileName).Order()];

                return [];
            }
            catch (Exception)
            {
                return [];
            }
        }
    }
}