using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public sealed record GenerateImageOptions : IGenerateOptions
    {
        public int Seed { get; set; }
        public string Prompt { get; set; }
        public string Prompt2 { get; set; }
        public string NegativePrompt { get; set; }
        public float GuidanceScale { get; set; } = 1;
        public float GuidanceScale2 { get; set; } = 1;
        public int Steps { get; set; } = 50;
        public int Steps2 { get; set; } = 20;
        public int Height { get; set; }
        public int Width { get; set; }
        public float Strength { get; set; } = 1;
        public float ControlNetScale { get; set; } = 1;
        public ControlNetType ControlType { get; set; }
        public string TempFileName { get; set; }
        public bool EnableVaeTiling { get; set; }
        public bool EnableVaeSlicing { get; set; }
        public LanguageType Language { get; set; }
        public string Instruction { get; set; }
        public string Task { get; set; }
        public int MaxLength { get; set; }
        public int MaxLength2 { get; set; }
        public LatentUpscale LatentUpscale { get; set; }
        public int LatentUpscaleSteps { get; set; }
        public float LatentUpscaleStrength { get; set; }
        public int LatentUpscaleTileSize { get; set; }
        public int ClipSkip { get; set; }
        public bool EnableCircularX { get; set; }
        public bool EnableCircularY { get; set; }
        public SamplerOptions SamplerOptions { get; set; }
        public DiffusionCacheOptions CacheOptions { get; set; }
        public List<LoraOptions> LoraOptions { get; set; }



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
