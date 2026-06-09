using Amuse.App.Services;
using System.Linq;
using System.Runtime.CompilerServices;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public sealed record DeviceModel : Device
    {
        public DeviceModel() { }
        public DeviceModel(Device options, GPUDevice gpuDevice) : base(options)
        {
            PCIBusId = gpuDevice.PCIBusId;
            DeviceType = gpuDevice.DeviceType;
            QualityModes = GetQualityModes(Vendor);
            SupportedBackends = GetSupportedBackends(Vendor, DeviceType);
            DefaultQualityMode = QualityModes.Contains(QualityMode.Standard) ? QualityMode.Standard : QualityMode.Production;
        }

        public int PCIBusId { get; init; }
        public string DeviceType { get; init; }
        public QualityMode[] QualityModes { get; init; }
        public QualityMode DefaultQualityMode { get; init; }
        public BackendType[] SupportedBackends { get; init; }


        private static QualityMode[] GetQualityModes(VendorType vendor)
        {
            return vendor switch
            {
                VendorType.AMD => [QualityMode.Draft, QualityMode.Standard, QualityMode.Production],
                VendorType.Nvidia => [QualityMode.Draft, QualityMode.Standard, QualityMode.Production],
                _ => [QualityMode.Production]
            };
        }


        private static BackendType[] GetSupportedBackends(VendorType vendor, string deviceType)
        {
            if (vendor == VendorType.Intel)
                return [BackendType.OnnxRuntime];

            return [BackendType.PyTorch, BackendType.OnnxRuntime];
        }


        public bool Equals(DeviceModel other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
