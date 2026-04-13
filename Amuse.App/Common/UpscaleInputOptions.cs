using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class UpscaleInputOptions : BaseModel
    {
        private bool _isTileEnabled;
        private int _tileSize;
        private int _tileOverlap;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTileEnabled
        {
            get { return _isTileEnabled; }
            set { SetProperty(ref _isTileEnabled, value); }
        }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TileSize
        {
            get { return _tileSize; }
            set { SetProperty(ref _tileSize, value); }
        }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TileOverlap
        {
            get { return _tileOverlap; }
            set { SetProperty(ref _tileOverlap, value); }
        }
    }
}
