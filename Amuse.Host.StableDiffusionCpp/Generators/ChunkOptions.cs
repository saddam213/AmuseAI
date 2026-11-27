namespace Amuse.Host.StableDiffusionCpp
{
    /// <summary>
    /// The options of a video generated in chunks.
    /// </summary>
    /// <param name="ChunkFrames">The frames to generate per chunk.</param>
    /// <param name="IsColorMatchEnabled">Whether the end of each chunk is colour matched to the first chunk.</param>
    /// <param name="Strength">How strongly a chunk holds the frame it starts from, zero keeps the strength of the video. Below 1 the model redraws some of the frame, which stops softness building up from chunk to chunk.</param>
    internal sealed record ChunkOptions(int ChunkFrames, bool IsColorMatchEnabled, float Strength);
}
