namespace Amuse.Common
{
    /// <summary>
    /// The options for a video interpolation stream run on a backend host, the frames are passed through the stream channel.
    /// </summary>
    public sealed record InterpolateVideoRunOptions
    {
        public int Multiplier { get; set; }
        public int FrameCount { get; set; }
        public float FrameRate { get; set; }
    }
}
