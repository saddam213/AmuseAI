using System;

namespace Amuse.Common
{
    /// <summary>
    /// The frame counts a video pipeline accepts, a count is the offset plus a multiple of the step and at least the minimum.
    /// Only pipelines that can start from a previous frame generate long videos in chunks, a model of the pipeline also needs a chunk size to offer it.
    /// </summary>
    /// <param name="Step">The spacing between counts.</param>
    /// <param name="Offset">The count added to a multiple of the step.</param>
    /// <param name="Minimum">The smallest count.</param>
    /// <param name="IsChunkSupported">Whether long videos can be generated in chunks.</param>
    public sealed record VideoFrameRule(int Step, int Offset, int Minimum, bool IsChunkSupported)
    {
        /// <summary>
        /// Gets the frame rule of a pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public static VideoFrameRule Get(PipelineType pipeline)
        {
            return pipeline switch
            {
                PipelineType.MiniMaxVideoPipeline => new VideoFrameRule(17, 5, 5, true),
                PipelineType.LTX20Pipeline => new VideoFrameRule(8, 1, 9, true),
                PipelineType.WanPipeline => new VideoFrameRule(4, 1, 5, true),
                PipelineType.HunyuanVideoPipeline => new VideoFrameRule(4, 1, 5, false),
                _ => new VideoFrameRule(1, 0, 1, false)
            };
        }


        /// <summary>
        /// Snaps the frames to the nearest count of the rule, no larger than the maximum.
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="maximum">The largest count allowed.</param>
        public int Snap(int frames, int maximum)
        {
            var minimum = Math.Min(Minimum, maximum);
            var lowest = Offset + (int)Math.Ceiling((minimum - Offset) / (double)Step) * Step;
            var highest = Offset + (int)Math.Floor((maximum - Offset) / (double)Step) * Step;
            var snapped = Offset + (int)Math.Round((Math.Clamp(frames, minimum, maximum) - Offset) / (double)Step) * Step;
            return Math.Clamp(snapped, lowest, Math.Max(lowest, highest));
        }


        /// <summary>
        /// Aligns the frames up to the next count of the rule.
        /// </summary>
        /// <param name="frames">The frames.</param>
        public int AlignUp(int frames)
        {
            if (frames <= Minimum)
                return Minimum;

            return Offset + (int)Math.Ceiling((frames - Offset) / (double)Step) * Step;
        }
    }
}
