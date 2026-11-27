using Amuse.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TensorStack.WPF;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for FrameCountControl.xaml
    /// The length of a video entered in seconds or in frames, the value is snapped to a frame count the pipeline accepts and shown in the other unit beside it, next to the frame rate that converts between them, the frame rate can only be changed for a model that allows it.
    /// A model that can generate in chunks has an option to allow it, the length is then no longer limited to a single pass and the chunk size is entered in the same unit.
    /// </summary>
    public partial class FrameCountControl : UserControl
    {
        private const int MaximumChunkedFrames = 1000000;
        private readonly Settings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameCountControl"/> class.
        /// </summary>
        public FrameCountControl()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
                _settings = App.GetService<Settings>();

            FrameRates = [8f, 12f, 16f, 24f, 25f, 30f, 60f];
            ToggleModeCommand = new AsyncRelayCommand(ToggleModeAsync);
            InitializeComponent();
            IsFrameMode = _settings?.IsFrameCountInFrames ?? false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty FramesProperty = DependencyProperty.Register(nameof(Frames), typeof(int), typeof(FrameCountControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty ChunkFramesProperty = DependencyProperty.Register(nameof(ChunkFrames), typeof(int), typeof(FrameCountControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty IsChunkAllowedProperty = DependencyProperty.Register(nameof(IsChunkAllowed), typeof(bool), typeof(FrameCountControl), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChunkAllowedChanged));
        public static readonly DependencyProperty FrameRateProperty = DependencyProperty.Register(nameof(FrameRate), typeof(float), typeof(FrameCountControl), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty IsFrameRateEnabledProperty = DependencyProperty.Register(nameof(IsFrameRateEnabled), typeof(bool), typeof(FrameCountControl));
        public static readonly DependencyProperty MaxFramesProperty = DependencyProperty.Register(nameof(MaxFrames), typeof(int), typeof(FrameCountControl));
        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineType), typeof(FrameCountControl), new PropertyMetadata(default(PipelineType), OnChunkSupportChanged));
        public static readonly DependencyProperty IsChunkEnabledProperty = DependencyProperty.Register(nameof(IsChunkEnabled), typeof(bool), typeof(FrameCountControl), new PropertyMetadata(false, OnChunkSupportChanged));
        public static readonly DependencyProperty IsChunkSupportedProperty = DependencyProperty.Register(nameof(IsChunkSupported), typeof(bool), typeof(FrameCountControl));
        public static readonly DependencyProperty IsFrameModeProperty = DependencyProperty.Register(nameof(IsFrameMode), typeof(bool), typeof(FrameCountControl), new PropertyMetadata(false, OnValueChanged));

        public int Frames
        {
            get { return (int)GetValue(FramesProperty); }
            set { SetValue(FramesProperty, value); }
        }

        public int ChunkFrames
        {
            get { return (int)GetValue(ChunkFramesProperty); }
            set { SetValue(ChunkFramesProperty, value); }
        }

        public bool IsChunkAllowed
        {
            get { return (bool)GetValue(IsChunkAllowedProperty); }
            set { SetValue(IsChunkAllowedProperty, value); }
        }

        public float FrameRate
        {
            get { return (float)GetValue(FrameRateProperty); }
            set { SetValue(FrameRateProperty, value); }
        }

        public bool IsFrameRateEnabled
        {
            get { return (bool)GetValue(IsFrameRateEnabledProperty); }
            set { SetValue(IsFrameRateEnabledProperty, value); }
        }

        public int MaxFrames
        {
            get { return (int)GetValue(MaxFramesProperty); }
            set { SetValue(MaxFramesProperty, value); }
        }

        public PipelineType Pipeline
        {
            get { return (PipelineType)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }

        public bool IsChunkEnabled
        {
            get { return (bool)GetValue(IsChunkEnabledProperty); }
            set { SetValue(IsChunkEnabledProperty, value); }
        }

        public bool IsChunkSupported
        {
            get { return (bool)GetValue(IsChunkSupportedProperty); }
            private set { SetValue(IsChunkSupportedProperty, value); }
        }

        public bool IsFrameMode
        {
            get { return (bool)GetValue(IsFrameModeProperty); }
            set { SetValue(IsFrameModeProperty, value); }
        }

        public List<float> FrameRates { get; }
        public AsyncRelayCommand ToggleModeCommand { get; }


        /// <summary>
        /// Switches between seconds and frames, the choice is saved and shared by every view.
        /// </summary>
        private async Task ToggleModeAsync()
        {
            CommitFrames();
            CommitChunkFrames();
            if (_settings == null)
            {
                IsFrameMode = !IsFrameMode;
                return;
            }

            _settings.IsFrameCountInFrames = !_settings.IsFrameCountInFrames;
            await _settings.SaveAsync();
        }


        /// <summary>
        /// Reads the length and snaps it to a frame count the model accepts, text that is not a number restores the current value.
        /// The length is limited to a single pass unless chunking is allowed.
        /// </summary>
        private void CommitFrames()
        {
            if (TryReadFrames(TextBoxValue.Text, out var frames))
                Frames = VideoFrameRule.Get(Pipeline).Snap(frames, GetLengthLimit());

            UpdateText();
        }


        /// <summary>
        /// Reads the chunk size and snaps it to a frame count the model generates in a single pass, text that is not a number restores the current value.
        /// </summary>
        private void CommitChunkFrames()
        {
            if (IsChunkSupported && TryReadFrames(TextBoxChunk.Text, out var frames))
                ChunkFrames = VideoFrameRule.Get(Pipeline).Snap(frames, GetMaximumFrames());

            UpdateText();
        }


        /// <summary>
        /// Gets the largest frame count the model generates in a single pass.
        /// </summary>
        private int GetMaximumFrames()
        {
            return MaxFrames > 0 ? MaxFrames : MaximumChunkedFrames;
        }


        /// <summary>
        /// Gets the largest length, a single pass unless chunking is allowed.
        /// </summary>
        private int GetLengthLimit()
        {
            return IsChunkSupported && IsChunkAllowed ? MaximumChunkedFrames : GetMaximumFrames();
        }


        /// <summary>
        /// Reads text entered in the current unit as a frame count.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="frames">The frames.</param>
        private bool TryReadFrames(string text, out int frames)
        {
            frames = 0;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value <= 0)
                return false;

            var frameRate = FrameRate > 0 ? FrameRate : 1f;
            var count = IsFrameMode ? value : value * frameRate;
            frames = (int)Math.Round(Math.Min(count, MaximumChunkedFrames));
            return true;
        }


        /// <summary>
        /// Formats a frame count in the current unit.
        /// </summary>
        /// <param name="frames">The frames.</param>
        private string FormatFrames(int frames)
        {
            if (IsFrameMode || FrameRate <= 0)
                return frames.ToString(CultureInfo.CurrentCulture);

            return (frames / FrameRate).ToString("0.##", CultureInfo.CurrentCulture);
        }


        /// <summary>
        /// Formats the length in the unit that is not being entered, the duration in frame mode and the frame count in seconds mode.
        /// </summary>
        private string FormatDetail()
        {
            if (!IsFrameMode || FrameRate <= 0)
                return Frames.ToString(CultureInfo.CurrentCulture);

            var duration = TimeSpan.FromSeconds(Frames / FrameRate);
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture)
                : duration.ToString(@"mm\:ss\.ff", CultureInfo.CurrentCulture);
        }


        /// <summary>
        /// Shows the length, its detail and the chunk size in the current unit.
        /// </summary>
        private void UpdateText()
        {
            TextBoxValue.Text = FormatFrames(Frames);
            TextBoxDetail.Text = FormatDetail();
            TextBoxChunk.Text = FormatFrames(ChunkFrames);
        }


        /// <summary>
        /// Called when the frames, chunk size, frame rate or mode changed.
        /// </summary>
        /// <param name="d">The control.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameCountControl control)
                control.UpdateText();
        }


        /// <summary>
        /// Called when chunking is allowed or disallowed, a length above a single pass is brought back within it when chunking is no longer allowed.
        /// </summary>
        /// <param name="d">The control.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnChunkAllowedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameCountControl control && control.Frames > control.GetLengthLimit())
                control.Frames = VideoFrameRule.Get(control.Pipeline).Snap(control.Frames, control.GetLengthLimit());
        }


        /// <summary>
        /// Called when the pipeline or the model's chunk setting changed, chunking needs a pipeline that supports it and a model that has a chunk size.
        /// </summary>
        /// <param name="d">The control.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnChunkSupportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameCountControl control)
                control.IsChunkSupported = control.IsChunkEnabled && VideoFrameRule.Get(control.Pipeline).IsChunkSupported;
        }


        /// <summary>
        /// Called when the control is loaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_settings == null)
                return;

            _settings.PropertyChanged -= Settings_PropertyChanged;
            _settings.PropertyChanged += Settings_PropertyChanged;
            IsFrameMode = _settings.IsFrameCountInFrames;
        }


        /// <summary>
        /// Called when the control is unloaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
                _settings.PropertyChanged -= Settings_PropertyChanged;
        }


        /// <summary>
        /// Called when a setting changed, another view may have switched the mode.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.IsFrameCountInFrames))
                IsFrameMode = _settings.IsFrameCountInFrames;
        }


        /// <summary>
        /// Commits the length when its text box loses focus.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void TextBoxValue_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitFrames();
        }


        /// <summary>
        /// Commits the chunk size when its text box loses focus.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void TextBoxChunk_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitChunkFrames();
        }


        /// <summary>
        /// Commits the value of a text box on Enter.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not TextBox textBox)
                return;

            if (textBox == TextBoxChunk)
                CommitChunkFrames();
            else
                CommitFrames();

            textBox.SelectAll();
            e.Handled = true;
        }
    }
}
