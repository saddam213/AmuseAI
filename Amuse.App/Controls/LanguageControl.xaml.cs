using Amuse.Common;
using System;
using System.Windows;
using TensorStack.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for LanguageControl.xaml
    /// </summary>
    public partial class LanguageControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageControl"/> class.
        /// </summary>
        public LanguageControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(LanguageControl));
        public static readonly DependencyProperty SelectedLanguageProperty = DependencyProperty.Register(nameof(SelectedLanguage), typeof(LanguageType), typeof(LanguageControl), new FrameworkPropertyMetadata(LanguageType.English, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty SupportedLanguagesProperty = DependencyProperty.Register(nameof(SupportedLanguages), typeof(LanguageType[]), typeof(LanguageControl), new FrameworkPropertyMetadata(Enum.GetValues<LanguageType>(), null, CoerceSupportedLanguages));

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public LanguageType[] SupportedLanguages
        {
            get { return (LanguageType[])GetValue(SupportedLanguagesProperty); }
            set { SetValue(SupportedLanguagesProperty, value); }
        }


        public LanguageType SelectedLanguage
        {
            get { return (LanguageType)GetValue(SelectedLanguageProperty); }
            set { SetValue(SelectedLanguageProperty, value); }
        }


        private static object CoerceSupportedLanguages(DependencyObject d, object baseValue)
        {
            if (baseValue is LanguageType[] languages && !languages.IsNullOrEmpty())
                return languages;
            return Enum.GetValues<LanguageType>();
        }
    }
}