using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public sealed record GenerateTextOptions : IGenerateOptions
    {
        public int Seed { get; set; }
        public string Prompt { get; set; }
        public List<ConversationMessage> Conversation { get; set; }
        public string TempFileName { get; set; }
        public LanguageType Language { get; set; }
        public string Instruction { get; set; }
        public string Task { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public bool IsSamplingEnabled { get; set; }
        public int Beams { get; set; }
        public float Temperature { get; set; }
        public int TopK { get; set; }
        public float TopP { get; set; }
        public float TopH { get; set; }
        public float TypicalP { get; set; }
        public float RepetitionPenalty { get; set; }
        public float LengthPenalty { get; set; }
        public int NoRepeatNgramSize { get; set; }
        public string EarlyStopping { get; set; }
        public int ChunkSize { get; set; }
        public bool IsThinkingEnabled { get; set; }
        public int SampleRate { get; set; }
        public CacheType CacheType { get; set; }
        public string[] Tools { get; set; }

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
