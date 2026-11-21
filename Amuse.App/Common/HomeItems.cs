using TensorStack.Common;

namespace Amuse.App.Common
{
    /// <summary>
    /// The state of a backend on a device, shown on the Home view.
    /// </summary>
    public enum HomeBackendStatus
    {
        /// <summary>
        /// The device does not support the backend.
        /// </summary>
        Unsupported = 0,

        /// <summary>
        /// The backend is supported but its environment is not installed yet.
        /// </summary>
        NotInstalled = 1,

        /// <summary>
        /// The backend is ready to use.
        /// </summary>
        Ready = 2
    }


    /// <summary>
    /// A backend and its state on a device.
    /// </summary>
    /// <param name="Backend">The backend.</param>
    /// <param name="Status">The status.</param>
    public sealed record HomeBackendItem(BackendType Backend, HomeBackendStatus Status);


    /// <summary>
    /// A device and the state of each backend on it.
    /// </summary>
    /// <param name="Device">The device.</param>
    /// <param name="Backends">The backends.</param>
    public sealed record HomeDeviceItem(DeviceModel Device, HomeBackendItem[] Backends);
}
