using System.Collections.Generic;
using TensorStack.Common.Tensor;

namespace Amuse.Common.Message
{
    internal interface IPipelineMessage
    {
        IReadOnlyList<Tensor<float>> Tensors { get; set; }
    }

}
