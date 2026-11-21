using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    /// <summary>
    /// The options for an image extract run on a backend host.
    /// </summary>
    public sealed record ExtractImageRunOptions : IGenerateOptions
    {
        public bool IsTileEnabled { get; set; }
        public int TileSize { get; set; }
        public int TileOverlap { get; set; }
        public bool IsInverted { get; set; }
        public bool MergeInput { get; set; }
        public BackgroundMode Mode { get; set; }
        public bool IsTransparentSupported { get; set; } = true;
        public int Detections { get; set; }
        public float BodyConfidence { get; set; }
        public float JointConfidence { get; set; }
        public float ColorAlpha { get; set; }
        public float JointRadius { get; set; }
        public float BoneRadius { get; set; }
        public float BoneThickness { get; set; }
        public bool IsTransparent { get; set; }

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
