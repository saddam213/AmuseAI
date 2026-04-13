using Amuse.App.Common;
using System.Windows;
using System.Windows.Controls;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for StatisticsControl.xaml
    /// </summary>
    public partial class StatisticsControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatisticsControl"/> class.
        /// </summary>
        public StatisticsControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(ProgressInfo), typeof(StatisticsControl));
        public static readonly DependencyProperty StatisticsProperty = DependencyProperty.Register(nameof(Statistics), typeof(StatisticsModel), typeof(StatisticsControl));


        public ProgressInfo Progress
        {
            get { return (ProgressInfo)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public StatisticsModel Statistics
        {
            get { return (StatisticsModel)GetValue(StatisticsProperty); }
            set { SetValue(StatisticsProperty, value); }
        }
    }
}
