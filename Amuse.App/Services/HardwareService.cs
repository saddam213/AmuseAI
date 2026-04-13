using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using TensorStack.Common;
using TensorStack.Providers;

namespace Amuse.App.Services
{
    public sealed class HardwareService : IHardwareService
    {
        private readonly int _refreshRate = 500;
        private readonly CPUDevice _cpuDevice;
        private readonly NPUDevice _npuDevice;
        private readonly GPUDevice[] _gpuDevices;
        private readonly ManagementObjectSearcher _objectSearcherDriver;
        private readonly ManagementObjectSearcher _objectSearcherProcessor;
        private readonly ManagementObjectSearcher _objectSearcherGPUEngine;
        private readonly ManagementObjectSearcher _objectSearcherGPUMemory;
        private readonly ManagementObjectSearcher _objectSearcherGPUProcessMemory;
        private readonly ManagementObjectSearcher _objectSearcherProcessorPercent;
        private readonly ManualResetEvent _updateThreadResetEvent;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Settings _hardwareSettings;
        private Thread _cpuUpdateThread;
        private Thread _gpuUpdateThread;
        private CPUStatus _cpuStatus;
        private NPUStatus _npuStatus;
        private GPUStatus[] _gpuStatus;
        private AdapterInfo[] _adapters;
        private DeviceInfo[] _deviceInfo;

        public HardwareService(Settings hardwareSettings)
        {
            Provider.Initialize();
            _hardwareSettings = hardwareSettings;
            _cancellationTokenSource = new CancellationTokenSource();
            _objectSearcherDriver = new ManagementObjectSearcher("root\\CIMV2", "SELECT DeviceName, DriverVersion, Location FROM Win32_PnPSignedDriver");
            _objectSearcherProcessor = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor");
            _objectSearcherGPUEngine = new ManagementObjectSearcher("root\\CIMV2", $"SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            _objectSearcherGPUMemory = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name, SharedUsage, DedicatedUsage, TotalCommitted FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory");
            _objectSearcherGPUProcessMemory = new ManagementObjectSearcher("root\\CIMV2", $"SELECT Name, SharedUsage, DedicatedUsage, TotalCommitted FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory");
            _objectSearcherProcessorPercent = new ManagementObjectSearcher("root\\CIMV2", "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            _adapters = hardwareSettings.IsLegacyDeviceDetection
                ? DeviceInterop.GetAdaptersLegacy()
                : DeviceInterop.GetAdapters();
            _deviceInfo = GetDeviceInfo();
            _cpuDevice = CreateDeviceCPU();
            _gpuDevices = CreateDeviceGPU();
            _npuDevice = CreateDeviceNPU();
            _updateThreadResetEvent = new ManualResetEvent(true);
            CreateUpdateThreadCPU(_refreshRate, _cancellationTokenSource.Token);
            CreateUpdateThreadGPU(_refreshRate, _cancellationTokenSource.Token);
        }


        public CPUDevice CPUDevice => _cpuDevice;
        public CPUStatus CPUStatus => _cpuStatus;

        public NPUDevice NPUDevice => _npuDevice;
        public NPUStatus NPUStatus => _npuStatus;

        public GPUDevice[] GPUDevices => _gpuDevices ?? [];
        public GPUStatus[] GPUStatus => _gpuStatus ?? [];

        public AdapterInfo[] Adapters => _adapters ?? [];


        public IReadOnlyList<DeviceModel> GetGPUDevices()
        {
            var outputDevices = new List<DeviceModel>();
            var providerDevices = Provider.GetDevices();
            foreach (var device in GPUDevices)
            {
                var providerDevice = providerDevices.FirstOrDefault(x => x.HardwareID == device.AdapterInfo.DeviceId && x.HardwareVendorId == device.AdapterInfo.VendorId);
                if (providerDevice == null)
                    continue;

                outputDevices.Add(new DeviceModel(providerDevice, device));
            }
            return outputDevices;
        }


        private CPUDevice CreateDeviceCPU()
        {
            try
            {
                string cpuName = "Unknown CPU";
                using (var results = _objectSearcherProcessor.Get())
                {
                    foreach (var result in results)
                    {
                        using (result)
                        {
                            cpuName = result["Name"].ToString().Trim();
                        }
                    }
                }

                var deviceInfo = _deviceInfo.FirstOrDefault(d => d.Name == cpuName);
                var memStatus = DeviceInterop.GetMemoryStatus();
                return new CPUDevice
                {
                    Name = cpuName,
                    MemoryTotal = memStatus.TotalPhysicalMemory / 1024f / 1024f
                };
            }
            catch (Exception)
            {
                return default;
            }
        }


        private CPUStatus CreateStatusCPU()
        {
            try
            {
                ulong processorUsage = 0;
                using (var results = _objectSearcherProcessorPercent.Get())
                {
                    foreach (var result in results)
                    {
                        using (result)
                        {
                            processorUsage = (ulong)result["PercentProcessorTime"];
                        }
                    }
                }

                var memStatus = DeviceInterop.GetMemoryStatus();
                return new CPUStatus(_cpuDevice)
                {
                    Usage = (int)processorUsage,
                    MemoryUsage = (int)memStatus.MemoryLoad,
                    MemoryAvailable = memStatus.AvailablePhysicalMemory / 1024f / 1024f
                };
            }
            catch (Exception)
            {
                return default;
            }
        }


        private GPUDevice[] CreateDeviceGPU()
        {
            try
            {
                var devices = new List<GPUDevice>();
                foreach (var device in _adapters.Where(x => x.Type == AdapterType.GPU))
                {
                    var deviceInfo = _deviceInfo.FirstOrDefault(d => d.Name == device.Description);
                    devices.Add(new GPUDevice(device, deviceInfo));
                }
                return devices.ToArray();
            }
            catch (Exception)
            {
                return [];
            }
        }


        private GPUStatus[] CreateStatusGPU(Dictionary<string, GPUUtilization[]> gpuUtilization, Dictionary<string, GPUMemory> gpuMemoryUsage, Dictionary<string, GPUMemory> gpuProcessUsage)
        {
            var results = new GPUStatus[_gpuDevices.Length];
            for (int i = 0; i < _gpuDevices.Length; i++)
            {
                try
                {
                    var device = _gpuDevices[i];
                    var result = new GPUStatus(device);

                    if (gpuMemoryUsage.TryGetValue(device.AdapterId, out var memoryUsage))
                    {
                        result.MemoryUsage = memoryUsage.DedicatedUsage / 1024f / 1024f;
                        result.SharedMemoryUsage = memoryUsage.SharedUsage / 1024f / 1024f;
                    }

                    if (gpuProcessUsage.TryGetValue(device.AdapterId, out var processUsage))
                    {
                        result.ProcessMemoryTotal = processUsage.DedicatedUsage / 1024f / 1024f;
                        result.ProcessSharedMemoryUsage = processUsage.SharedUsage / 1024f / 1024f;
                    }

                    if (gpuUtilization.TryGetValue(device.AdapterId, out var deviceUtilization))
                    {
                        var utilization = deviceUtilization
                            .GroupBy(x => x.Instance)
                            .ToDictionary(u => u.Key, y => y.Sum(x => (int)x.Utilization));
                        utilization.TryGetValue("Cuda", out var engineCuda);
                        utilization.TryGetValue("3D", out var engineGraphics);
                        utilization.TryGetValue("Graphics1", out var engineGraphics1);
                        utilization.TryGetValue("Compute", out var engineCompute);
                        utilization.TryGetValue("Compute0", out var engineCompute0);
                        utilization.TryGetValue("Compute1", out var engineCompute1);
                        result.UsageCuda = engineCuda;
                        result.UsageCompute = Math.Max(engineCompute, engineCompute0);
                        result.UsageCompute1 = engineCompute1;
                        result.UsageGraphics = engineGraphics;
                        result.UsageGraphics1 = engineGraphics1;
                    }

                    results[i] = result;
                }
                catch (Exception)
                {

                }
            }
            return results;
        }


        private NPUDevice CreateDeviceNPU()
        {
            try
            {
                var devices = new List<NPUDevice>();
                foreach (var device in _adapters.Where(x => x.IsHardware && x.Type == AdapterType.NPU))
                {
                    var deviceInfo = _deviceInfo.FirstOrDefault(d => d.Name == device.Description);
                    devices.Add(new NPUDevice(device, deviceInfo));
                }
                return devices.FirstOrDefault();
            }
            catch (Exception)
            {
                return default;
            }
        }


        private NPUStatus CreateStatusNPU(Dictionary<string, GPUUtilization[]> gpuUtilization, Dictionary<string, GPUMemory> gpuMemoryUsage, Dictionary<string, GPUMemory> gpuProcessUsage)
        {
            try
            {
                if (_npuDevice == default)
                    return default;

                var result = new NPUStatus(_npuDevice);
                if (gpuMemoryUsage.TryGetValue(_npuDevice.AdapterId, out var memoryUsage))
                {
                    result.MemoryUsage = memoryUsage.SharedUsage / 1024f / 1024f;
                }

                if (gpuProcessUsage.TryGetValue(_npuDevice.AdapterId, out var processUsage))
                {
                    result.ProcessMemoryTotal = processUsage.DedicatedUsage / 1024f / 1024f;
                    result.ProcessSharedMemoryUsage = processUsage.SharedUsage / 1024f / 1024f;
                }

                if (gpuUtilization.TryGetValue(_npuDevice.AdapterId, out var deviceUtilization))
                {
                    var utilization = deviceUtilization
                        .GroupBy(x => x.Instance)
                        .ToDictionary(u => u.Key, y => y.Sum(x => (int)x.Utilization));
                    utilization.TryGetValue("Compute", out var engineCompute);
                    result.Usage = engineCompute > 100 ? engineCompute - 100 : engineCompute;
                }
                return result;
            }
            catch (Exception)
            {
                return default;
            }
        }


        private Dictionary<string, GPUMemory> GetMemoryUsageGPU()
        {
            var gpuMemory = new Dictionary<string, GPUMemory>();
            try
            {
                using (var results = _objectSearcherGPUMemory.Get())
                {
                    foreach (var result in results)
                    {
                        using (result)
                        {
                            var adapterId = result["Name"]?.ToString();
                            var sharedUsage = (ulong)result.Properties["SharedUsage"].Value;
                            var dedicatedUsage = (ulong)result.Properties["DedicatedUsage"].Value;
                            var totalCommitted = (ulong)result.Properties["TotalCommitted"].Value;
                            gpuMemory[adapterId] = new GPUMemory(adapterId, sharedUsage, dedicatedUsage, totalCommitted, 0);
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
            return gpuMemory;
        }



        private Dictionary<string, GPUMemory> GetProcessMemoryUsageGPU()
        {
            var gpuMemory = new Dictionary<string, GPUMemory>();
            try
            {
                using (var results = _objectSearcherGPUProcessMemory.Get())
                {
                    foreach (var result in results)
                    {
                        using (result)
                        {
                            var parts = result.Properties["Name"].Value.ToString().Split('_');
                            int.TryParse(parts[1], out int pid);
                            var adapterId = string.Join('_', parts.Skip(2));
                            var sharedUsage = (ulong)result.Properties["SharedUsage"].Value;
                            var dedicatedUsage = (ulong)result.Properties["DedicatedUsage"].Value;
                            var totalCommitted = (ulong)result.Properties["TotalCommitted"].Value;
                            gpuMemory[adapterId] = new GPUMemory(adapterId, sharedUsage, dedicatedUsage, totalCommitted, pid);
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
            return gpuMemory;
        }



        private Dictionary<string, GPUUtilization[]> GetUtilizationGPU()
        {
            var gpuUtilization = new List<GPUUtilization>();
            try
            {
                using (var results = _objectSearcherGPUEngine.Get())
                {
                    foreach (var result in results)
                    {
                        using (result)
                        {
                            var percentage = (ulong)result.Properties["UtilizationPercentage"].Value;
                            if (percentage == 0)
                                continue;

                            string instanceName = result["Name"]?.ToString();
                            if (instanceName.Contains("engtype_Cuda") || instanceName.Contains("engtype_3D") || instanceName.Contains("engtype_Graphics") || instanceName.Contains("engtype_Compute"))
                            {
                                var parts = result.Properties["Name"].Value.ToString().Split('_');
                                int.TryParse(parts[1], out int pid);
                                var instance = string.Concat(parts.Skip(10)).Replace(" ", string.Empty);
                                var adapterId = string.Join('_', parts.Skip(2).Take(5));
                                gpuUtilization.Add(new GPUUtilization(adapterId, instance, percentage, pid));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

            }

            return gpuUtilization
                .GroupBy(x => x.AdapterId)
                .ToDictionary(k => k.Key, v => v.ToArray());
        }


        private DeviceInfo[] GetDeviceInfo()
        {
            try
            {
                var versions = new List<DeviceInfo>();
                var devices = _adapters.Select(x => x.Description).ToArray();

                using (var results = _objectSearcherDriver.Get())
                {
                    foreach (var result in results)
                    {
                        var deviceName = result["DeviceName"]?.ToString() ?? string.Empty;
                        if (!devices.Contains(deviceName))
                            continue;

                        var location = result["Location"]?.ToString() ?? string.Empty;
                        var driverVersion = result["DriverVersion"]?.ToString() ?? string.Empty;
                        versions.Add(new DeviceInfo(deviceName.Trim(), driverVersion, location));
                    }
                }

                return versions.ToArray();
            }
            catch (Exception)
            {
                return [];
            }
        }


        private void CreateUpdateThreadCPU(int refreshRate, CancellationToken cancellationToken)
        {
            _cpuUpdateThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        _updateThreadResetEvent.WaitOne();
                        cancellationToken.ThrowIfCancellationRequested();

                        var timestamp = Stopwatch.GetTimestamp();
                        _cpuStatus = CreateStatusCPU();
                        var eplased = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                        Thread.Sleep(refreshRate);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ThreadInterruptedException) { break; }
                }
            });
            _cpuUpdateThread.IsBackground = true;
            _cpuUpdateThread.Start();
        }


        private void CreateUpdateThreadGPU(int refreshRate, CancellationToken cancellationToken)
        {
            _gpuUpdateThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        _updateThreadResetEvent.WaitOne();
                        cancellationToken.ThrowIfCancellationRequested();

                        var timestamp = Stopwatch.GetTimestamp();
                        var gpuProcessMemoryUsage = GetProcessMemoryUsageGPU();
                        var gpuMemoryUsage = GetMemoryUsageGPU();
                        var gpuUtilization = GetUtilizationGPU();
                        _gpuStatus = CreateStatusGPU(gpuUtilization, gpuMemoryUsage, gpuProcessMemoryUsage);
                        _npuStatus = CreateStatusNPU(gpuUtilization, gpuMemoryUsage, gpuProcessMemoryUsage);
                        var eplased = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                        Thread.Sleep(refreshRate);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ThreadInterruptedException) { break; }
                }
            });
            _gpuUpdateThread.IsBackground = true;
            _gpuUpdateThread.Start();
        }


        /// <summary>
        /// Pauses this instance.
        /// </summary>
        public void Pause()
        {
            _updateThreadResetEvent.Reset();
        }


        /// <summary>
        /// Resumes this instance.
        /// </summary>
        public void Resume()
        {
            _updateThreadResetEvent.Set();
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _updateThreadResetEvent.Set();
            _cpuUpdateThread.Join();
            _gpuUpdateThread.Join();
            _updateThreadResetEvent.Dispose();
            _cancellationTokenSource.Dispose();
        }

        private record struct GPUUtilization(string AdapterId, string Instance, ulong Utilization, int ProcessId);
        private record struct GPUMemory(string AdapterId, ulong SharedUsage, ulong DedicatedUsage, ulong TotalCommitted, int ProcessId);
    }


    public interface IHardwareService : IDisposable
    {
        CPUDevice CPUDevice { get; }
        CPUStatus CPUStatus { get; }

        NPUDevice NPUDevice { get; }
        NPUStatus NPUStatus { get; }

        GPUDevice[] GPUDevices { get; }
        GPUStatus[] GPUStatus { get; }

        AdapterInfo[] Adapters { get; }

        void Pause();
        void Resume();

        IReadOnlyList<DeviceModel> GetGPUDevices();
    }



    public record CPUDevice
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public float MemoryTotal { get; set; }
    }

    public record struct CPUStatus
    {
        public CPUStatus(CPUDevice device)
        {
            Id = device.Id;
            Name = device.Name;
            MemoryTotal = device.MemoryTotal;
        }

        public int Id { get; }
        public string Name { get; }

        public float MemoryTotal { get; }




        public int Usage { get; set; }
        public int MemoryUsage { get; set; }
        public float MemoryAvailable { get; set; }
    }

    public record struct DeviceInfo(string Name, string DriverVersion, string Location);

    public record GPUDevice
    {
        public GPUDevice(AdapterInfo adapter, DeviceInfo deviceInfo)
        {
            Id = (int)adapter.Id;
            AdapterInfo = adapter;
            Name = adapter.Description;
            Location = deviceInfo.Location;
            PCIBusId = GetBusId(deviceInfo.Location);
            DriverVersion = deviceInfo.DriverVersion;
            MemoryTotal = adapter.DedicatedVideoMemory / 1024f / 1024f;
            SharedMemoryTotal = adapter.SharedSystemMemory / 1024f / 1024f;
            AdapterId = $"luid_0x{adapter.AdapterLuid.HighPart:X8}_0x{adapter.AdapterLuid.LowPart:X8}_phys_0";
        }

        public int Id { get; }
        public string Name { get; }
        public string Location { get; }
        public int PCIBusId { get; }
        public string AdapterId { get; }
        public string DriverVersion { get; }
        public float MemoryTotal { get; }
        public float SharedMemoryTotal { get; }
        public AdapterInfo AdapterInfo { get; }

        private static int GetBusId(string location)
        {
            var pciBusId = location.Split(',').First();
            if (string.IsNullOrWhiteSpace(pciBusId))
                return 0;

            if (!int.TryParse(pciBusId.Replace("pci bus ", "", StringComparison.OrdinalIgnoreCase), out var pciid))
                return 0;

            return pciid;
        }
    }


    public record struct GPUStatus
    {
        public GPUStatus(GPUDevice device)
        {
            Id = device.Id;
            Name = device.Name;
            MemoryTotal = device.MemoryTotal;
            SharedMemoryTotal = device.SharedMemoryTotal;
        }

        public int Id { get; set; }
        public string Name { get; set; }

        public float MemoryTotal { get; set; }
        public float MemoryUsage { get; set; }

        public float SharedMemoryTotal { get; set; }
        public float SharedMemoryUsage { get; set; }

        public float ProcessMemoryTotal { get; set; }
        public float ProcessSharedMemoryUsage { get; set; }

        public int UsageCuda { get; set; }
        public int UsageCompute { get; set; }
        public int UsageCompute1 { get; set; }
        public int UsageGraphics { get; set; }
        public int UsageGraphics1 { get; set; }
    }

    public record NPUDevice
    {
        public NPUDevice(AdapterInfo adapter, DeviceInfo deviceInfo)
        {
            Id = (int)adapter.Id;
            AdapterInfo = adapter;
            Name = adapter.Description;
            DriverVersion = deviceInfo.DriverVersion;
            MemoryTotal = adapter.SharedSystemMemory / 1024f / 1024f;
            AdapterId = $"luid_0x{adapter.AdapterLuid.HighPart:X8}_0x{adapter.AdapterLuid.LowPart:X8}_phys_0";
        }

        public int Id { get; }
        public string Name { get; }
        public string AdapterId { get; }
        public string DriverVersion { get; }
        public float MemoryTotal { get; }
        public AdapterInfo AdapterInfo { get; }
    }

    public record struct NPUStatus
    {
        public NPUStatus(NPUDevice device)
        {
            Id = device.Id;
            Name = device.Name;
            MemoryTotal = device.MemoryTotal;
        }

        public int Id { get; set; }
        public string Name { get; set; }

        public int Usage { get; set; }
        public float MemoryTotal { get; set; }
        public float MemoryUsage { get; set; }

        public float ProcessMemoryTotal { get; set; }
        public float ProcessSharedMemoryUsage { get; set; }
    }

    public static class DeviceInterop
    {
        [DllImport("OnnxStack.Adapter.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetAdapters([In, Out] AdapterInfo[] adapterArray);

        [DllImport("OnnxStack.Adapter.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetAdaptersLegacy([In, Out] AdapterInfo[] adapterArray);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);


        public static AdapterInfo[] GetAdapters()
        {
            var adaptersDX = GetAdaptersLegacy();
            var adaptersDXCore = GetAdaptersCore();
            var mergedSet = new HashSet<AdapterInfo>(new LuidComparer());
            for (int i = 0; i < adaptersDX.Length; i++)
            {
                var adapterDX = adaptersDX[i];
                var adapterDXCore = adaptersDXCore.FirstOrDefault(x => x.AdapterLuid == adapterDX.AdapterLuid);
                if (adapterDXCore.DeviceId != 0)
                {
                    adapterDX.Type = adapterDXCore.Type;
                    adapterDX.IsDetachable = adapterDXCore.IsDetachable;
                    adapterDX.IsIntegrated = adapterDXCore.IsIntegrated;
                    adapterDX.IsHardware = adapterDXCore.IsHardware;
                    adapterDX.IsLegacy = adapterDXCore.IsLegacy;
                    mergedSet.Add(adapterDX);
                    continue;
                }
                mergedSet.Add(adapterDX);
            }

            mergedSet.UnionWith(adaptersDXCore);
            return mergedSet.ToArray();
        }


        public static AdapterInfo[] GetAdaptersCore()
        {
            var adapters = new AdapterInfo[20];
            GetAdapters(adapters);

            var uniqueSet = new HashSet<AdapterInfo>(new LuidComparer());
            foreach (var adapter in adapters)
            {
                if (adapter.DeviceId == 0)
                    continue;
                if (adapter.DeviceId == 140 && adapter.VendorId == 5140)
                    continue;

                uniqueSet.Add(adapter);
            }
            return uniqueSet.ToArray();
        }


        public static AdapterInfo[] GetAdaptersLegacy()
        {
            var adapters = new AdapterInfo[20];
            GetAdaptersLegacy(adapters);

            var uniqueSet = new HashSet<AdapterInfo>(new LuidComparer());
            foreach (var adapter in adapters)
            {
                if (adapter.DeviceId == 0)
                    continue;
                if (adapter.DeviceId == 140 && adapter.VendorId == 5140)
                    continue;

                uniqueSet.Add(adapter);
            }
            return uniqueSet.ToArray();
        }


        public static MemoryStatusEx GetMemoryStatus()
        {
            var memStatus = new MemoryStatusEx();
            GlobalMemoryStatusEx(ref memStatus);
            return memStatus;
        }

    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.None)]
    public struct AdapterInfo
    {
        public uint Id;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public ulong DedicatedVideoMemory;
        public ulong DedicatedSystemMemory;
        public ulong SharedSystemMemory;
        public Luid AdapterLuid;
        public AdapterType Type;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsHardware;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsIntegrated;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsDetachable;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;

        [MarshalAs(UnmanagedType.I1)]
        public bool IsLegacy;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;

        public override bool Equals(object obj)
        {
            return obj is Luid other && this == other;
        }

        public static bool operator ==(Luid left, Luid right)
        {
            return left.LowPart == right.LowPart && left.HighPart == right.HighPart;
        }


        public static bool operator !=(Luid left, Luid right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LowPart, HighPart);
        }
    }

    public class LuidComparer : IEqualityComparer<AdapterInfo>
    {
        public bool Equals(AdapterInfo x, AdapterInfo y)
        {
            return x.AdapterLuid.Equals(y.AdapterLuid);
        }

        public int GetHashCode(AdapterInfo obj)
        {
            return obj.AdapterLuid.GetHashCode();
        }
    }



    [Flags]
    public enum AdapterFlags : uint
    {
        None = 0,
        Remote = 1,
        Software = 2
    }

    public enum AdapterType : uint
    {
        GPU = 0,
        NPU = 1,
        Other = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }
}
