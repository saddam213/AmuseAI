using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed record UpscaleInputOptions : BaseRecord
    {
        private bool _isTileEnabled;
        private int _tileSize;
        private int _tileOverlap;
        private long _seed;
        private int _windowSize;
        private int _temporalChunkSize;

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


        /// <summary>
        /// Gets or sets the video upscale seed.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long Seed
        {
            get { return _seed; }
            set { SetProperty(ref _seed, value); }
        }


        /// <summary>
        /// Gets or sets the video upscale window size.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int WindowSize
        {
            get { return _windowSize; }
            set { SetProperty(ref _windowSize, value); }
        }


        /// <summary>
        /// Gets or sets the video upscale temporal chunk size.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TemporalChunkSize
        {
            get { return _temporalChunkSize; }
            set { SetProperty(ref _temporalChunkSize, value); }
        }


        public UpscaleInputOptions DeepClone()
        {
            return new UpscaleInputOptions
            {
                IsTileEnabled = IsTileEnabled,
                TileOverlap = TileOverlap,
                TileSize = TileSize,
                Seed = Seed,
                WindowSize = WindowSize,
                TemporalChunkSize = TemporalChunkSize
            };
        }

        public bool Equals(UpscaleInputOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
