using System;
using System.Windows;
using System.Windows.Controls;
using TensorStack.WPF;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for SeedControl.xaml
    /// </summary>
    public partial class SeedControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeedControl"/> class.
        /// </summary>
        public SeedControl()
        {
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            InitializeComponent();
        }

        public static readonly DependencyProperty SeedProperty = DependencyProperty.Register(nameof(Seed), typeof(int), typeof(SeedControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int Seed
        {
            get { return (int)GetValue(SeedProperty); }
            set { SetValue(SeedProperty, value); }
        }

        public RelayCommand<bool> SeedCommand { get; }


        private void GenerateSeed(bool random)
        {
            Seed = random ? 0 : Random.Shared.Next();
        }
    }
}
