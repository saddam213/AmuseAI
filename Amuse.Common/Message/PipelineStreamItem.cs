using System.Collections.Generic;
using TensorStack.Common.Tensor;

namespace Amuse.Common.Message
{
    /// <summary>
    /// A single item of a pipeline stream, a video frame, an audio chunk or a text event.
    /// </summary>
    public sealed record PipelineStreamItem
    {
        public int Index { get; init; }
        public float Rate { get; init; }
        public string Text { get; init; }
        public IReadOnlyList<Tensor<float>> Tensors { get; init; } = [];
    }
}
