using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class LoraOptionModel : BaseModel
    {
        private string _key;
        private string _name;
        private float _strength;

        public string Key
        {
            get { return _key; }
            init { SetProperty(ref _key, value); }
        }

        public string Name
        {
            get { return _name; }
            init { SetProperty(ref _name, value); }
        }

        public float Strength
        {
            get { return _strength; }
            set { SetProperty(ref _strength, value); }
        }
    }
}
