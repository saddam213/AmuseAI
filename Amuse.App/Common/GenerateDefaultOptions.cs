using Amuse.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.OnnxRuntime.LLM.Common;

namespace Amuse.App.Common
{
    public sealed record GenerateDefaultOptions
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float GuidanceScale { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float GuidanceScale2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Steps { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Steps2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Height { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Width { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Frames { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float FrameRate { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FrameChunk { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FrameChunkOverlap { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NoiseCondition { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Strength { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SchedulerType Scheduler { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SchedulerInputOptions[] Schedulers { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Channels { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SampleRate { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] FrameOptions { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsVaeTilingEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsVaeSlicingEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsFirstFrameLastFrameEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MinLength { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxLength { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxLength2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Duration { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int BPM { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Speed { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SilenceDuration { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NoRepeatNgramSize { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSamplingEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsThinkingEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Beams { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TopK { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TopP { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Temperature { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float LengthPenalty { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public EarlyStopping EarlyStopping { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DiversityLength { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TopH { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TypicalP { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float RepetitionPenalty { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ChunkSize { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Task { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] Tasks { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public LanguageType Language { get; set; } = LanguageType.English;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LanguageType[] Languages { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public CacheType CacheType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int InputImageMaxCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int InputAudioMaxCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int InputVideoMaxCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsFlashAttentionEnabled { get; set; } = true;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LatentUpscaleSteps { get; set; } = 4;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float LatentUpscaleStrength { get; set; } = 0.7f;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LatentUpscaleTileSize { get; set; } = 128;

        public GenerateDefaultOptions DeepClone()
        {
            return new GenerateDefaultOptions
            {
                Width = Width,
                Height = Height,
                Steps = Steps,
                Steps2 = Steps2,
                GuidanceScale = GuidanceScale,
                GuidanceScale2 = GuidanceScale2,
                Frames = Frames,
                FrameRate = FrameRate,
                SampleRate = SampleRate,
                FrameChunk = FrameChunk,
                FrameChunkOverlap = FrameChunkOverlap,
                FrameOptions = FrameOptions?.ToArray(),
                NoiseCondition = NoiseCondition,
                Scheduler = Scheduler,
                Schedulers = Schedulers.Copy(),
                Strength = Strength,
                IsVaeSlicingEnabled = IsVaeSlicingEnabled,
                IsVaeTilingEnabled = IsVaeTilingEnabled,
                IsFirstFrameLastFrameEnabled = IsFirstFrameLastFrameEnabled,
                MaxLength = MaxLength,
                MaxLength2 = MaxLength2,
                Channels = Channels,
                Beams = Beams,
                BPM = BPM,
                ChunkSize = ChunkSize,
                DiversityLength = DiversityLength,
                Duration = Duration,
                EarlyStopping = EarlyStopping,
                LengthPenalty = LengthPenalty,
                MinLength = MinLength,
                NoRepeatNgramSize = NoRepeatNgramSize,
                SilenceDuration = SilenceDuration,
                Speed = Speed,
                Task = Task,
                Tasks = Tasks?.ToArray(),
                Temperature = Temperature,
                TopK = TopK,
                TopP = TopP,
                Language = Language,
                Languages = Languages?.ToArray(),
                IsSamplingEnabled = IsSamplingEnabled,
                IsThinkingEnabled = IsThinkingEnabled,
                RepetitionPenalty = RepetitionPenalty,
                TopH = TopH,
                TypicalP = TypicalP,
                CacheType = CacheType,
                InputAudioMaxCount = InputAudioMaxCount,
                InputImageMaxCount = InputImageMaxCount,
                InputVideoMaxCount = InputVideoMaxCount,
                IsFlashAttentionEnabled = IsFlashAttentionEnabled
            };
        }

        public bool Equals(GenerateDefaultOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
