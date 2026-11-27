using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public sealed record GenerateAudioOptions : IGenerateOptions
    {
        public int Seed { get; set; }
        public string Prompt { get; set; }
        public string Prompt2 { get; set; }
        public string NegativePrompt { get; set; }
        public float GuidanceScale { get; set; } = 1;
        public float GuidanceScale2 { get; set; } = 1;
        public int Steps { get; set; } = 50;
        public int Steps2 { get; set; } = 20;
        public float Strength { get; set; } = 1;
        public string TempFileName { get; set; }
        public bool EnableVaeTiling { get; set; }
        public bool EnableVaeSlicing { get; set; }
        public float Duration { get; set; } = 5f;
        public LanguageType Language { get; set; }
        public string Instruction { get; set; }
        public string Task { get; set; }
        public int MaxLength { get; set; }
        public int MaxLength2 { get; set; }
        public int Bpm { get; set; }
        public string Keyscale { get; set; }
        public string TrackName { get; set; }
        public string TimeSignature { get; set; }
        public float Speed { get; set; }
        public float SilenceDuration { get; set; }
        public int SampleRate { get; set; }
        public float RangeStart { get; set; }
        public float RangeEnd { get; set; }
        public string ReferenceText { get; set; }
        public string Emotion { get; set; }
        public bool HasReferenceAudio { get; set; }
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
