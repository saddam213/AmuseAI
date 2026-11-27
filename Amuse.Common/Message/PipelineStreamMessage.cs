using System.Collections.Generic;

namespace Amuse.Common.Message
{
    /// <summary>
    /// The header sent over the stream pipe, item tensors are passed through shared memory.
    /// </summary>
    public sealed record PipelineStreamMessage
    {
        public int StreamId { get; init; }
        public PipelineStreamMessageType Type { get; init; }
        public int Index { get; init; }
        public float Rate { get; init; }
        public string Text { get; init; }
        public string Error { get; init; }
        public string MapName { get; init; }
        public List<int[]> Dimensions { get; init; }
    }


    public enum PipelineStreamMessageType
    {
        Ready = 0,
        Item = 1,
        Ack = 2,
        End = 3
    }
}
