using TensorStack.Common;

namespace Amuse.Common
{
    /// <summary>
    /// The upscale model configuration sent to a backend host when an upscale pipeline loads.
    /// </summary>
    public sealed record UpscaleConfig
    {
        public PipelineType Pipeline { get; set; }
        public DeviceType DeviceType { get; set; }
        public string ModelPath { get; set; }
        public string DiffusionModelPath { get; set; }
        public string VaePath { get; set; }
        public int ScaleFactor { get; set; }
        public int TileSize { get; set; }
        public int Channels { get; set; } = 3;
        public int SampleSize { get; set; }
        public Normalization Normalization { get; set; } = Normalization.ZeroToOne;
        public Normalization OutputNormalization { get; set; } = Normalization.OneToOne;
    }
}
