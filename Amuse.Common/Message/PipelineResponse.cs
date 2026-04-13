using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;

namespace Amuse.Common.Message
{
    public class PipelineResponse : IPipelineMessage
    {
        public PipelineResponse() { }
        public PipelineResponse(Exception ex) : this(ex.Message)
        {
            IsCanceled = ex is OperationCanceledException;
        }
        public PipelineResponse(string errorMessage)
        {
            Error = errorMessage;
        }

        public PipelineResponse(params IReadOnlyList<Tensor<float>> tensors)
        {
            Tensors = tensors;
        }

        public string Error { get; init; }
        public bool IsCanceled { get; init; }

        [JsonIgnore]
        public IReadOnlyList<Tensor<float>> Tensors { get; set; }


        [JsonIgnore]
        public bool IsError => !string.IsNullOrEmpty(Error);
    }
}
