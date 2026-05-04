using Amuse.App.Views;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public interface IHistoryItem
    {
        string Id { get; init; }
        int Version { get; init; }
        View Source { get; init; }
        MediaType MediaType { get; init; }
        DateTime Timestamp { get; init; }
        string Extension { get; init; }

        int Width { get; init; }
        int Height { get; init; }
        float FrameRate { get; init; }
        int FrameCount { get; init; }
        int SampleRate { get; init; }
        TimeSpan Duration { get; init; }

        string Model { get; init; }

        string FilePath { get; set; }
        string MediaPath { get; set; }
        string ThumbPath { get; set; }
        DateTime LastAccess { get; set; }
    }


    public record RecentHistory : IHistoryItem
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
        public string OriginalPath { get; init; }

        [JsonIgnore]
        public string FilePath { get; set; }

        [JsonIgnore]
        public string MediaPath { get; set; }

        [JsonIgnore]
        public string ThumbPath { get; set; }

        public virtual bool Equals(RecentHistory other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
