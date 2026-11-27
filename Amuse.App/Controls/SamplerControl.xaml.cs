using Amuse.App.Common;
using System.Windows;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for SamplerControl.xaml
    /// </summary>
    public partial class SamplerControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamplerControl"/> class.
        /// </summary>
        public SamplerControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(SamplerInputOptions[]), typeof(SamplerControl));
        public static readonly DependencyProperty SelectedOptionsProperty = DependencyProperty.Register(nameof(SelectedOptions), typeof(SamplerInputOptions), typeof(SamplerControl));
        public bool IsSelectorOnly { get; set; }

        public SamplerInputOptions[] Options
        {
            get { return (SamplerInputOptions[])GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public SamplerInputOptions SelectedOptions
        {
            get { return (SamplerInputOptions)GetValue(SelectedOptionsProperty); }
            set { SetValue(SelectedOptionsProperty, value); }
        }
    }
}
