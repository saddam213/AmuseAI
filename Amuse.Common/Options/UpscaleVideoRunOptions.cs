using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    /// <summary>
    /// The options for a video upscale run on a backend host, SeedVR2 and RelayVSR.
    /// </summary>
    public sealed record UpscaleVideoRunOptions : IGenerateOptions
    {
        public long Seed { get; set; }
        public int WindowSize { get; set; }
        public int TemporalChunkSize { get; set; }

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
