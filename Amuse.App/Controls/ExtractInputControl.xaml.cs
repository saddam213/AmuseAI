using Amuse.App.Common;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for ExtractInputControl.xaml
    /// </summary>
    public partial class ExtractInputControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractInputControl"/> class.
        /// </summary>
        public ExtractInputControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(ExtractInputControl), new PropertyMetadata<ExtractInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(ExtractInputOptions), typeof(ExtractInputControl), new PropertyMetadata<ExtractInputControl>((c) => c.OnOptionsChanged()));
        public static readonly DependencyProperty ExtractorTypeProperty = DependencyProperty.Register(nameof(ExtractorType), typeof(ExtractorType), typeof(ExtractInputControl), new PropertyMetadata<ExtractInputControl>((c) => c.OnExtractorTypeChanged()));

        public PipelineModel Pipeline
        {
            get { return (PipelineModel)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }

        public ExtractInputOptions Options
        {
            get { return (ExtractInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public ExtractorType ExtractorType
        {
            get { return (ExtractorType)GetValue(ExtractorTypeProperty); }
            set { SetValue(ExtractorTypeProperty, value); }
        }


        private Task OnOptionsChanged()
        {
            return Task.CompletedTask;
        }


        private Task OnExtractorTypeChanged()
        {
            return Task.CompletedTask;
        }


        private Task OnPipelineChanged(PipelineModel oldPipeline, PipelineModel newPipeline)
        {
            if (newPipeline is null || newPipeline.ExtractModel is null)
                return Task.CompletedTask;

            var previousOptions = Options;
            var oldModel = oldPipeline?.ExtractModel;
            var newModel = newPipeline.ExtractModel;
            var newOptions = newModel.DefaultOptions;

            if (oldModel == newModel)
                return Task.CompletedTask;

            // ExtractModel
            if (newModel is not null)
            {
                Options = new ExtractInputOptions
                {
                    IsTileEnabled = newOptions.IsTileEnabled,
                    TileSize = newOptions.TileSize,
                    TileOverlap = newOptions.TileOverlap,
                    IsInverted = newOptions.IsInverted,
                    IsTransparent = newOptions.IsTransparent,
                    MergeInput = newOptions.MergeInput,
                    Mode = newOptions.Mode,
                    Detections = newOptions.Detections,
                    BodyConfidence = newOptions.BodyConfidence,
                    JointConfidence = newOptions.JointConfidence,
                    ColorAlpha = newOptions.ColorAlpha,
                    JointRadius = newOptions.JointRadius,
                    BoneRadius = newOptions.BoneRadius,
                    BoneThickness = newOptions.BoneThickness
                };

                ExtractorType = newModel.Type;
            }
            return Task.CompletedTask;
        }
    }
}
