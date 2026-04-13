using System.Windows;

namespace Amuse.App
{
    /// <summary>
    /// Interaction logic for Splashscreen.xaml
    /// </summary>
    public partial class Splashscreen : Window
    {
        public Splashscreen()
        {
            Title = $"Amuse {App.AppVersionTag} Starting...";
            InitializeComponent();
            Show();
        }
    }
}
