using Amuse.App.Common;
using Amuse.App.Views;
using Amuse.Common;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for UpscaleInputControl.xaml
    /// </summary>
    public partial class UpscaleInputControl : BaseControl
    {
        private const int DefaultVideoWindowSize = 16;
        private const int DefaultVideoTemporalChunkSize = 1;
        private InputTabOption _selectedOption;
        private bool _isTileOptionsVisible = true;
        private bool _isVideoOptionsVisible;
        private bool _isTileSizeOptionVisible;
        private bool _isTemporalChunkOptionVisible;
        private bool _isWindowSizeOptionVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleInputControl"/> class.
        /// </summary>
        public UpscaleInputControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(UpscaleInputControl), new PropertyMetadata<UpscaleInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(UpscaleInputOptions), typeof(UpscaleInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(UpscaleInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(UpscaleInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(UpscaleInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(UpscaleInputControl));

        public View ViewType { get; set; }
        public PipelineModel Pipeline
        {
            get { return (PipelineModel)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }


        public UpscaleInputOptions Options
        {
            get { return (UpscaleInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public AutomationOptions AutomationOptions
        {
            get { return (AutomationOptions)GetValue(AutomationOptionsProperty); }
            set { SetValue(AutomationOptionsProperty, value); }
        }

        public ProgressInfo AutomationProgress
        {
            get { return (ProgressInfo)GetValue(AutomationProgressProperty); }
            set { SetValue(AutomationProgressProperty, value); }
        }

        public bool IsExecuting
        {
            get { return (bool)GetValue(IsExecutingProperty); }
            set { SetValue(IsExecutingProperty, value); }
        }

        public bool IsAutomating
        {
            get { return (bool)GetValue(IsAutomatingProperty); }
            set { SetValue(IsAutomatingProperty, value); }
        }

        public InputTabOption SelectedOption
        {
            get { return _selectedOption; }
            set { SetProperty(ref _selectedOption, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tile options are visible, OnnxRuntime upscalers.
        /// </summary>
        public bool IsTileOptionsVisible
        {
            get { return _isTileOptionsVisible; }
            set { SetProperty(ref _isTileOptionsVisible, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the video upscale options are visible, video upscalers.
        /// </summary>
        public bool IsVideoOptionsVisible
        {
            get { return _isVideoOptionsVisible; }
            set { SetProperty(ref _isVideoOptionsVisible, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the tile size option is visible, StableDiffusionCpp image upscalers.
        /// </summary>
        public bool IsTileSizeOptionVisible
        {
            get { return _isTileSizeOptionVisible; }
            set { SetProperty(ref _isTileSizeOptionVisible, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the temporal chunk size option is visible, SeedVR2 upscalers.
        /// </summary>
        public bool IsTemporalChunkOptionVisible
        {
            get { return _isTemporalChunkOptionVisible; }
            set { SetProperty(ref _isTemporalChunkOptionVisible, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the window size option is visible, RelayVSR upscalers.
        /// </summary>
        public bool IsWindowSizeOptionVisible
        {
            get { return _isWindowSizeOptionVisible; }
            set { SetProperty(ref _isWindowSizeOptionVisible, value); }
        }


        private Task OnPipelineChanged(PipelineModel oldPipeline, PipelineModel newPipeline)
        {
            if (newPipeline is null || newPipeline.UpscaleModel is null)
                return Task.CompletedTask;

            var previousOptions = Options;
            var oldModel = oldPipeline?.UpscaleModel;
            var newModel = newPipeline.UpscaleModel;
            var newOptions = newModel.DefaultOptions;

            if (oldModel == newModel)
                return Task.CompletedTask;

            // UpscaleModel
            if (newModel is not null)
            {
                IsVideoOptionsVisible = newModel.IsVideoUpscaler;
                IsTileOptionsVisible = !newModel.IsVideoUpscaler && newModel.Backend == BackendType.OnnxRuntime;
                IsTileSizeOptionVisible = !newModel.IsVideoUpscaler && newModel.Backend == BackendType.StableDiffusionCpp;
                IsTemporalChunkOptionVisible = newModel.Pipeline == PipelineType.SeedVR2Pipeline;
                IsWindowSizeOptionVisible = newModel.Pipeline == PipelineType.RelayVSRPipeline;
                // Only the options the model uses, history shows what is set
                var isImageUpscaler = !newModel.IsVideoUpscaler;
                Options = new UpscaleInputOptions
                {
                    IsTileEnabled = isImageUpscaler && (newOptions?.IsTileEnabled ?? false),
                    TileSize = isImageUpscaler ? newOptions?.TileSize ?? 0 : 0,
                    TileOverlap = isImageUpscaler && newModel.Backend == BackendType.OnnxRuntime ? newOptions?.TileOverlap ?? 0 : 0,
                    Seed = newModel.IsVideoUpscaler ? newOptions?.Seed ?? 0 : 0,
                    WindowSize = IsWindowSizeOptionVisible ? GetDefaultValue(newOptions?.WindowSize, DefaultVideoWindowSize) : 0,
                    TemporalChunkSize = IsTemporalChunkOptionVisible ? GetDefaultValue(newOptions?.TemporalChunkSize, DefaultVideoTemporalChunkSize) : 0,
                };

                AutomationOptions = new AutomationOptions
                {
                    ViewType = ViewType,
                    UseInputSize = true,
                    Type = AutomationType.InputFiles,
                };
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// Gets the model default value, or the fallback when the model has none.
        /// </summary>
        /// <param name="value">The model default value.</param>
        /// <param name="fallback">The fallback value.</param>
        private static int GetDefaultValue(int? value, int fallback)
        {
            return value > 0 ? value.Value : fallback;
        }
    }

}