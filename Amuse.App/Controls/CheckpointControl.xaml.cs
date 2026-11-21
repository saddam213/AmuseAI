using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Windows;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for CheckpointControl.xaml
    /// </summary>
    public partial class CheckpointControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointControl"/> class.
        /// </summary>
        public CheckpointControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(CheckpointControl));
        public static readonly DependencyProperty CheckpointProperty = DependencyProperty.Register(nameof(Checkpoint), typeof(CheckpointComponent), typeof(CheckpointControl));
        public static readonly DependencyProperty IsDiffusionCheckpointProperty = DependencyProperty.Register(nameof(IsDiffusionCheckpoint), typeof(bool), typeof(CheckpointControl));
        public static readonly DependencyProperty SupportedTypesProperty = DependencyProperty.Register(nameof(SupportedTypes), typeof(CheckpointType[]), typeof(CheckpointControl), new PropertyMetadata(Enum.GetValues<CheckpointType>()));
        public static readonly DependencyProperty FileFilterProperty = DependencyProperty.Register(nameof(FileFilter), typeof(string), typeof(CheckpointControl), new PropertyMetadata("Model Files|*.safetensors;*.gguf;"));

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
    }
}