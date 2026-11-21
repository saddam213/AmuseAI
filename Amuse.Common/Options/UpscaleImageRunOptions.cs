using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    /// <summary>
    /// The options for an image upscale run on a backend host.
    /// </summary>
    public sealed record UpscaleImageRunOptions : IGenerateOptions
    {
        public bool IsTileEnabled { get; set; }
        public int TileSize { get; set; }
        public int TileOverlap { get; set; }

        [JsonIgnore]
        public List<ImageTensor> InputImages { get; set; } = [];

        [JsonIgnore]
        public List<ImageTensor> InputControlImages { get; set; } = [];

        [JsonIgnore]
        public List<AudioTensor> InputAudios { get; set; } = [];

        [JsonIgnore]
        public List<VideoSequence> InputVideos { get; set; } = [];
    }
}
