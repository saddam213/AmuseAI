using Amuse.App.Views;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Amuse.App.Common
{
    public record ImageLayerHistory : IHistoryItem
    {
        public string Id { get; init; }
        public int Version { get; init; }
        public View Source { get; init; }
        public MediaType MediaType { get; init; }
        public DateTime Timestamp { get; init; }
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


        public string Model { get; init; } = "Compose";


        [JsonIgnore]
        public string FilePath { get; set; }

        [JsonIgnore]
        public string MediaPath { get; set; }

        [JsonIgnore]
        public string ThumbPath { get; set; }

        public virtual bool Equals(ImageLayerHistory other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
