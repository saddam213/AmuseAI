using Amuse.App.Common;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for UpscaleInputControl.xaml
    /// </summary>
    public partial class UpscaleInputControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleInputControl"/> class.
        /// </summary>
        public UpscaleInputControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(UpscaleInputControl), new PropertyMetadata<UpscaleInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(UpscaleInputOptions), typeof(UpscaleInputControl));

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
                Options = new UpscaleInputOptions
                {
                    IsTileEnabled = newOptions.IsTileEnabled,
                    TileSize = newOptions.TileSize,
                    TileOverlap = newOptions.TileOverlap,
                };
            }
            return Task.CompletedTask;
        }
    }
}
