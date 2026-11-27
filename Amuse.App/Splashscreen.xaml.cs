using Amuse.App.Resources;
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
            UpdateTitle();
            InitializeComponent();
            Show();
        }


        /// <summary>
        /// Updates the title for the current UI language.
        /// </summary>
        public void UpdateTitle()
        {
            Title = string.Format(AppDefault.SplashStarting, App.AppVersionTag);
        }
    }
}
