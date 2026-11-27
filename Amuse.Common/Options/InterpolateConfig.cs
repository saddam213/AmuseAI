using TensorStack.Common;

namespace Amuse.Common
{
    /// <summary>
    /// The interpolation configuration sent to a backend host when an interpolation pipeline loads, the host uses the model in its own runtime folder.
    /// </summary>
    public sealed record InterpolateConfig
    {
        public DeviceType DeviceType { get; set; }
    }
}
