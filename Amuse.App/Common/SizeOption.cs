using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class SizeOption : BaseModel
    {
        private bool _isDefault;

        public int Width { get; init; }
        public int Height { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsDefault
        {
            get { return _isDefault; }
            set { SetProperty(ref _isDefault, value); }
        }


        [JsonIgnore]
        public AspectType Aspect
        {
            get
            {
                if (Width > Height)
                    return AspectType.Landscape;
                else if (Height > Width)
                    return AspectType.Portrait;
                return AspectType.Square;
            }
        }

        public override string ToString()
        {
            return $"{Width} x {Height}";
        }
    }
}
