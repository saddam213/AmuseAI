using Amuse.App.Common;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for SchedulerControl.xaml
    /// </summary>
    public partial class SchedulerControl : BaseControl
    {
        public SchedulerControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(SchedulerInputOptions[]), typeof(SchedulerControl), new PropertyMetadata<SchedulerControl, SchedulerInputOptions[]>((c, o, n) => c.OnOptionsChanged(o, n)));
        public static readonly DependencyProperty SelectedOptionsProperty = DependencyProperty.Register(nameof(SelectedOptions), typeof(SchedulerInputOptions), typeof(SchedulerControl), new PropertyMetadata<SchedulerControl, SchedulerInputOptions>((c, o, n) => c.OnSelectedOptionsChanged(o, n)));
        public bool IsSelectorOnly { get; set; }

        public SchedulerInputOptions[] Options
        {
            get { return (SchedulerInputOptions[])GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public SchedulerInputOptions SelectedOptions
        {
            get { return (SchedulerInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }


        private Task OnOptionsChanged(SchedulerInputOptions[] oldOptions, SchedulerInputOptions[] newOptions)
        {
            return Task.CompletedTask;
        }


        private Task OnSelectedOptionsChanged(SchedulerInputOptions oldOptions, SchedulerInputOptions newOptions)
        {
            return Task.CompletedTask;
        }
    }
}
