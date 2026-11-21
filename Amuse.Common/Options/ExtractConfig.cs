using TensorStack.Common;

namespace Amuse.Common
{
    /// <summary>
    /// The extract model configuration sent to a backend host when an extract pipeline loads.
    /// </summary>
    public sealed record ExtractConfig
    {
        public ExtractType Type { get; set; }
        public DeviceType DeviceType { get; set; }
        public string ModelPath { get; set; }
        public int Channels { get; set; } = 3;
        public int SampleSize { get; set; }
        public Normalization Normalization { get; set; } = Normalization.ZeroToOne;
        public Normalization OutputNormalization { get; set; } = Normalization.OneToOne;
        public int OutputChannels { get; set; } = 1;
        public bool IsDynamicOutput { get; set; }
    }
}
