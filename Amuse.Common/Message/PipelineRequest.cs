using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Python.Common;
using TensorStack.Python.Config;

namespace Amuse.Common.Message
{
    public class PipelineRequest : IPipelineMessage
    {
        public PipelineRequest() { }
        public PipelineRequest(RequestType type)
        {
            Type = type;
        }

        public PipelineRequest(PipelineConfig config, RequestType type)
        {
            PipelineConfig = config;
            Type = type;
        }

        public PipelineRequest(PipelineReloadOptions options)
        {
            PipelineReloadOptions = options;
            Type = RequestType.PipelineReload;
        }

        public PipelineRequest(EnvironmentConfig config, EnvironmentMode mode)
        {
            Environment = new EnvironmentRequest
            {
                Config = config,
                Mode = mode,
            };
            Type = RequestType.Environment;
        }

        public PipelineRequest(PipelineOptions options)
        {
            PipelineOptions = options;
            ImageTensorCount = options.InputImages?.Count ?? 0;
            ControlNetTensorCount = options.InputControlImages?.Count ?? 0;
            AudioTensorCount = options.InputAudios?.Count ?? 0;
            AudioSampleRate = options.InputAudios?.FirstOrDefault()?.SampleRate ?? 0;
            Tensors = GetInputTensors(options);
            Type = RequestType.PipelineRun;
        }


        public RequestType Type { get; init; }
        public EnvironmentRequest Environment { get; set; }
        public PipelineConfig PipelineConfig { get; set; }
        public PipelineReloadOptions PipelineReloadOptions { get; set; }
        public PipelineOptions PipelineOptions { get; set; }
        public int ImageTensorCount { get; set; }
        public int ControlNetTensorCount { get; set; }
        public int AudioTensorCount { get; set; }
        public int AudioSampleRate { get; set; }

        [JsonIgnore]
        public IReadOnlyList<Tensor<float>> Tensors { get; set; }


        private static List<Tensor<float>> GetInputTensors(PipelineOptions options)
        {
            var validTensors = new List<Tensor<float>>();
            void AddTensors(List<ImageTensor> tensors)
            {
                if (tensors.IsNullOrEmpty())
                    return;

                foreach (var tensor in tensors)
                {
                    if (tensor is not null)
                        validTensors.Add(tensor.GetChannels(3).ToTensor());
                }
            }

            AddTensors(options.InputImages);
            AddTensors(options.InputControlImages);
            validTensors.AddRange(options.InputAudios);
            if (validTensors.Count == 0)
                return default;

            return validTensors;
        }
    }

}
