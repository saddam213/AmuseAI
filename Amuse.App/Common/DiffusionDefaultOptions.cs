using System.Runtime.CompilerServices;
using TensorStack.Python.Scheduler;

namespace Amuse.App.Common
{
    public record DiffusionDefaultOptions
    {
        public float GuidanceScale { get; set; } = 0;
        public float GuidanceScale2 { get; set; } = 0;
        public int Steps { get; set; } = 50;
        public int Steps2 { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int Frames { get; set; }
        public float FrameRate { get; set; } = 0;
        public int FrameChunk { get; set; }
        public int FrameChunkOverlap { get; set; }
        public int NoiseCondition { get; set; }
        public float Strength { get; set; } = 0;
        public SchedulerType Scheduler { get; set; }
        public SchedulerSettings Schedulers { get; set; }
        public int SampleRate { get; set; } = 24000;
        public int[] FrameOptions { get; set; }
        public bool IsVaeTilingEnabled { get; set; }
        public bool IsVaeSlicingEnabled { get; set; }
        public bool IsFirstFrameLastFrameEnabled { get; set; }
        public int MaxLength { get; set; }
        public int MaxLength2 { get; set; }

        public virtual bool Equals(DiffusionDefaultOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
