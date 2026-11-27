using System.Collections.Generic;
using TensorStack.Common;


namespace Amuse.Common
{
    public sealed record PipelineLoadOptions
    {
        public string Template { get; set; }
        public string ModelType { get; set; }
        public PipelineType Pipeline { get; set; }
        public ProcessType ProcessType { get; set; }
        public string Device { get; set; }
        public int DeviceId { get; set; }
        public int DeviceBusId { get; set; }
        public VendorType DeviceVendor { get; set; }
        public int DeviceVendorIndex { get; set; }
        public string LoraAdapterPath { get; set; }
        public List<LoraConfig> LoraAdapters { get; set; }
        public ControlNetConfig ControlNet { get; set; }
        public MemoryModeType MemoryMode { get; set; }
        public CheckpointConfig CheckpointConfig { get; set; }
        public ExecutionOptions ExecutionOptions { get; set; }
        public UpscaleConfig UpscaleConfig { get; set; }
        public ExtractConfig ExtractConfig { get; set; }
        public InterpolateConfig InterpolateConfig { get; set; }
    }
}
