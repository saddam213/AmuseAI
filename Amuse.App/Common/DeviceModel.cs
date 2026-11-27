using Amuse.App.Services;
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
            SupportedBackends = GetSupportedBackends(Vendor, DeviceType);
        }

        public int PCIBusId => _baseDevice.PCIBusId;
        public string DeviceType => _baseDevice.DeviceType;
        public string DeviceCode { get; init; }
        public BackendType[] SupportedBackends { get; init; }
        public int VendorIndex { get; set; }

        private static BackendType[] GetSupportedBackends(VendorType vendor, string deviceType)
        {
            return [BackendType.OnnxRuntime, BackendType.StableDiffusionCpp, BackendType.AudioCpp, BackendType.LlamaCpp];
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
