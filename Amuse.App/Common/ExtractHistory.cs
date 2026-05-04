using Amuse.App.Views;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public record ExtractHistory : IHistoryItem
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
        public ExtractorType ExtractorType { get; init; }
        public ExtractInputOptions Options { get; init; }


        [JsonIgnore]
        public string FilePath { get; set; }

        [JsonIgnore]
        public string MediaPath { get; set; }

        [JsonIgnore]
        public string ThumbPath { get; set; }

        public virtual bool Equals(ExtractHistory other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
