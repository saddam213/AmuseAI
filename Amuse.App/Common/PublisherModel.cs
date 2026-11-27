using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// The company or author that publishes a model, shown with its logo in the Models view.
    /// </summary>
    public sealed class PublisherModel : BaseModel
    {
        private string _key;
        private string _name;
        private string _link;
        private string _icon;

        public string Key
        {
            get { return _key; }
            set { SetProperty(ref _key, value); }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Link
        {
            get { return _link; }
            set { SetProperty(ref _link, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Icon
        {
            get { return _icon; }
            set { SetProperty(ref _icon, value); NotifyPropertyChanged(nameof(IconSource)); }
        }

        [JsonIgnore]
        public string IconSource
        {
            get { return string.IsNullOrEmpty(_icon) ? null : $"pack://application:,,,/Amuse;component/Images/Publishers/{_icon}"; }
        }
    }
}
