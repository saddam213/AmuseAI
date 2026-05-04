using Amuse.App.Views;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public record DiffusionHistory : IHistoryItem
    {
        public string Id { get; init; }
        public int Version { get; init; }

        public View Source { get; init; }
        public MediaType MediaType { get; init; }
        public DateTime Timestamp { get; init; }
        public DateTime LastAccess { get; set; }
        public string Extension { get; init; }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Width { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Height { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float FrameRate { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FrameCount { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SampleRate { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan Duration { get; init; }


        public string Model { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string[] LoraModels { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ControlNetModel { get; init; }

        public DiffusionInputOptions Options { get; init; }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ExtractModel { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ExtractorType? ExtractorType { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ExtractInputOptions ExtractOptions { get; init; }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string UpscaleModel { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public UpscaleInputOptions UpscaleOptions { get; init; }


        [JsonIgnore]
        public string FilePath { get; set; }

        [JsonIgnore]
        public string MediaPath { get; set; }

        [JsonIgnore]
        public string ThumbPath { get; set; }


        public virtual bool Equals(DiffusionHistory other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
