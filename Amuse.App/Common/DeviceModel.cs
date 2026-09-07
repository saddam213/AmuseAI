using Amuse.App.Services;
using System.Linq;
using System.Runtime.CompilerServices;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public sealed record DeviceModel : Device
    {
        private readonly GPUDevice _baseDevice;

        public DeviceModel() { }
        public DeviceModel(Device options, GPUDevice gpuDevice) : base(options)
        {
            _baseDevice = gpuDevice;
            DeviceCode = GetDeviceCode(Vendor);
            QualityModes = GetQualityModes(Vendor);
            SupportedBackends = GetSupportedBackends(Vendor, DeviceType);
            DefaultQualityMode = QualityModes.Contains(QualityMode.Standard) ? QualityMode.Standard : QualityMode.Production;
            IsFlashAttentionEnabled = true;
        }

        public int PCIBusId => _baseDevice.PCIBusId;
        public string DeviceType => _baseDevice.DeviceType;
        public string DeviceCode { get; init; }
        public QualityMode[] QualityModes { get; init; }
        public QualityMode DefaultQualityMode { get; init; }
        public BackendType[] SupportedBackends { get; init; }
        public bool IsFlashAttentionEnabled { get; init; }
        public int VendorIndex { get; set; }

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
                return [BackendType.OnnxRuntime, BackendType.StableDiffusionCpp];

            return [BackendType.HuggingFace, BackendType.OnnxRuntime, BackendType.StableDiffusionCpp];
        }


        private static string GetDeviceCode(VendorType vendor)
        {
            if (vendor == VendorType.Intel)
                return "xpu";

            return "cuda";
        }


        public bool Equals(DeviceModel other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
