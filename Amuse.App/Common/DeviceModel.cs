using Amuse.App.Services;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public record DeviceModel : Device
    {
        public DeviceModel() { }
        public DeviceModel(Device options, GPUDevice gpuDevice) : base(options)
        {
            PCIBusId = gpuDevice.PCIBusId;
            IsLoraSupported = options.Vendor == VendorType.Nvidia || options.Vendor == VendorType.AMD;
            DefaultQualityMode = QualityMode.Standard;
            QualityModes = options.Vendor == VendorType.AMD
            ? [QualityMode.Draft, QualityMode.Standard, QualityMode.Production]
            : [QualityMode.Draft, QualityMode.Standard, QualityMode.Production];
        }

        public QualityMode[] QualityModes { get; init; }
        public bool IsLoraSupported { get; init; }
        public int PCIBusId { get; init; }
        public QualityMode DefaultQualityMode { get; init; }
    }
}
