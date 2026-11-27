using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using TensorStack.Common;
using TensorStack.OnnxRuntime;

namespace Amuse.App.Services
{
    public sealed class HardwareService : IHardwareService
    {
        private const string CounterGPUEngine = @"\GPU Engine(*)\Utilization Percentage";
        private const string CounterGPUDedicatedUsage = @"\GPU Adapter Memory(*)\Dedicated Usage";
        private const string CounterGPUSharedUsage = @"\GPU Adapter Memory(*)\Shared Usage";
        private const string CounterGPUProcessDedicatedUsage = @"\GPU Process Memory(*)\Dedicated Usage";
        private const string CounterGPUProcessSharedUsage = @"\GPU Process Memory(*)\Shared Usage";

        private readonly int _refreshRate = 500;
        private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(2);
        private readonly int _processId;
        private readonly CPUDevice _cpuDevice;
        private readonly NPUDevice _npuDevice;
        private readonly GPUDevice[] _gpuDevices;
        private readonly AdapterInfo[] _adapters;
        private readonly DeviceInfo[] _deviceInfo;
        private readonly ManualResetEvent _updateThreadResetEvent;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly PerformanceCounterQuery _gpuCounterQuery;
        private Thread _cpuUpdateThread;
        private Thread _gpuUpdateThread;
        private CPUStatus _cpuStatus;
        private NPUStatus _npuStatus;
        private GPUStatus[] _gpuStatus;
        private HashSet<int> _processIds;
        private CPUTimes _cpuTimes;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareService"/> class.
        /// </summary>
        /// <param name="hardwareSettings">The hardware settings.</param>
        public HardwareService(Settings hardwareSettings)
        {
            Provider.Initialize();
            _processId = Environment.ProcessId;
            _processIds = [_processId];
            _cancellationTokenSource = new CancellationTokenSource();
            _adapters = hardwareSettings.IsLegacyDeviceDetection
                ? DeviceInterop.GetAdaptersLegacy()
                : DeviceInterop.GetAdapters();
            _deviceInfo = GetDeviceInfo();
            _cpuDevice = CreateDeviceCPU();
            _gpuDevices = CreateDeviceGPU();
            _npuDevice = CreateDeviceNPU();
            _cpuTimes = DeviceInterop.GetCPUTimes();
            _gpuCounterQuery = new PerformanceCounterQuery(CounterGPUEngine, CounterGPUDedicatedUsage, CounterGPUSharedUsage, CounterGPUProcessDedicatedUsage, CounterGPUProcessSharedUsage);
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


        /// <summary>
        /// Gets the GPU devices supported by the execution providers.
        /// </summary>
        public IReadOnlyList<DeviceModel> GetGPUDevices()
        {
            var outputDevices = new Dictionary<VendorType, List<DeviceModel>>();
            var providerDevices = Provider.GetDevices();
            foreach (var providerDevice in providerDevices)
            {
                var gpuDevice = GPUDevices.FirstOrDefault(x => x.HardwareLUID == providerDevice.HardwareLUID);
                if (gpuDevice == null || gpuDevice.HardwareLUID == 0 || gpuDevice.HardwareLUID >= uint.MaxValue)
                    continue;

                if (!outputDevices.ContainsKey(providerDevice.Vendor))
                    outputDevices[providerDevice.Vendor] = new List<DeviceModel>();

                outputDevices[providerDevice.Vendor].Add(new DeviceModel(providerDevice, gpuDevice));
            }

            foreach (var vendorDevices in outputDevices.Values)
                foreach (var (index, vendorDevice) in vendorDevices.Index())
                    vendorDevice.VendorIndex = index;

            return [.. outputDevices.SelectMany(x => x.Value).OrderBy(x => x.Id)];
        }


        /// <summary>
        /// Creates the CPU device.
        /// </summary>
        private CPUDevice CreateDeviceCPU()
        {
            var cpuName = AppDefault.UnknownCPU;
            try
            {
                using (var objectSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor"))
                {
                    using (var results = objectSearcher.Get())
                    {
                        foreach (var result in results)
                        {
                            using (result)
                            {
                                cpuName = result["Name"]?.ToString().Trim() ?? cpuName;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Keep the default CPU name
            }

            var memStatus = DeviceInterop.GetMemoryStatus();
            return new CPUDevice
            {
                Name = cpuName,
                MemoryTotal = memStatus.TotalPhysicalMemory / 1024f / 1024f
            };
        }


        /// <summary>
        /// Creates the CPU status and refreshes the Amuse process tree.
        /// </summary>
        private CPUStatus CreateStatusCPU()
        {
            try
            {
                var cpuTimes = DeviceInterop.GetCPUTimes();
                var memStatus = DeviceInterop.GetMemoryStatus();
                var processIds = DeviceInterop.GetProcessTree(_processId);
                var result = new CPUStatus(_cpuDevice)
                {
                    Usage = GetUsageCPU(_cpuTimes, cpuTimes),
                    MemoryUsage = (int)memStatus.MemoryLoad,
                    MemoryAvailable = memStatus.AvailablePhysicalMemory / 1024f / 1024f,
                    ProcessMemoryUsage = DeviceInterop.GetWorkingSet(processIds) / 1024f / 1024f
                };
                _cpuTimes = cpuTimes;
                _processIds = processIds;
                return result;
            }
            catch (Exception)
            {
                return default;
            }
        }


        /// <summary>
        /// Gets the CPU usage percentage between two samples.
        /// </summary>
        /// <param name="previous">The previous sample.</param>
        /// <param name="current">The current sample.</param>
        private static int GetUsageCPU(CPUTimes previous, CPUTimes current)
        {
            var idle = current.Idle - previous.Idle;
            var total = (current.Kernel - previous.Kernel) + (current.User - previous.User);
            if (total == 0 || idle > total)
                return 0;

            return (int)Math.Round((total - idle) * 100.0 / total);
        }


        /// <summary>
        /// Creates the GPU devices.
        /// </summary>
        private GPUDevice[] CreateDeviceGPU()
        {
            try
            {
                return _adapters
                    .Where(x => x.Type == AdapterType.GPU)
                    .Select(x => new GPUDevice(x, _deviceInfo.FirstOrDefault(d => d.Name == x.Description)))
                    .ToArray();
            }
            catch (Exception)
            {
                return [];
            }
        }


        /// <summary>
        /// Creates the GPU status for each GPU device.
        /// </summary>
        /// <param name="gpuUtilization">The engine utilization per adapter.</param>
        /// <param name="gpuMemoryUsage">The memory usage per adapter.</param>
        /// <param name="gpuProcessUsage">The Amuse process memory usage per adapter.</param>
        private GPUStatus[] CreateStatusGPU(Dictionary<string, Dictionary<string, double>> gpuUtilization, Dictionary<string, GPUMemory> gpuMemoryUsage, Dictionary<string, GPUMemory> gpuProcessUsage)
        {
            var results = new GPUStatus[_gpuDevices.Length];
            for (int i = 0; i < _gpuDevices.Length; i++)
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
                    result.ProcessMemoryUsage = processUsage.DedicatedUsage / 1024f / 1024f;
                    result.ProcessSharedMemoryUsage = processUsage.SharedUsage / 1024f / 1024f;
                }

                if (gpuUtilization.TryGetValue(device.AdapterId, out var utilization))
                {
                    result.UsageCuda = GetEngineUsage(utilization, "Cuda");
                    result.UsageCompute = Math.Max(GetEngineUsage(utilization, "Compute"), GetEngineUsage(utilization, "Compute0"));
                    result.UsageCompute1 = GetEngineUsage(utilization, "Compute1");
                    result.UsageGraphics = GetEngineUsage(utilization, "3D");
                    result.UsageGraphics1 = GetEngineUsage(utilization, "Graphics1");
                }

                results[i] = result;
            }
            return results;
        }


        /// <summary>
        /// Creates the NPU device.
        /// </summary>
        private NPUDevice CreateDeviceNPU()
        {
            try
            {
                return _adapters
                    .Where(x => x.IsHardware && x.Type == AdapterType.NPU)
                    .Select(x => new NPUDevice(x, _deviceInfo.FirstOrDefault(d => d.Name == x.Description)))
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return default;
            }
        }


        /// <summary>
        /// Creates the NPU status.
        /// </summary>
        /// <param name="gpuUtilization">The engine utilization per adapter.</param>
        /// <param name="gpuMemoryUsage">The memory usage per adapter.</param>
        /// <param name="gpuProcessUsage">The Amuse process memory usage per adapter.</param>
        private NPUStatus CreateStatusNPU(Dictionary<string, Dictionary<string, double>> gpuUtilization, Dictionary<string, GPUMemory> gpuMemoryUsage, Dictionary<string, GPUMemory> gpuProcessUsage)
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
                result.ProcessMemoryUsage = processUsage.DedicatedUsage / 1024f / 1024f;
                result.ProcessSharedMemoryUsage = processUsage.SharedUsage / 1024f / 1024f;
            }

            if (gpuUtilization.TryGetValue(_npuDevice.AdapterId, out var utilization))
            {
                var engineCompute = GetEngineUsage(utilization, "Compute");
                result.Usage = engineCompute > 100 ? engineCompute - 100 : engineCompute;
            }
            return result;
        }


        /// <summary>
        /// Gets the memory usage of each adapter, in bytes.
        /// </summary>
        private Dictionary<string, GPUMemory> GetMemoryUsageGPU()
        {
            var gpuMemory = new Dictionary<string, GPUMemory>();
            try
            {
                var dedicatedUsage = _gpuCounterQuery.GetValues(CounterGPUDedicatedUsage);
                var sharedUsage = _gpuCounterQuery.GetValues(CounterGPUSharedUsage);
                foreach (var adapterId in dedicatedUsage.Keys.Union(sharedUsage.Keys))
                {
                    dedicatedUsage.TryGetValue(adapterId, out var dedicated);
                    sharedUsage.TryGetValue(adapterId, out var shared);
                    gpuMemory[adapterId] = new GPUMemory((ulong)shared, (ulong)dedicated);
                }
            }
            catch (Exception)
            {
                // Counters unavailable
            }
            return gpuMemory;
        }


        /// <summary>
        /// Gets the memory usage of the Amuse process tree on each adapter, in bytes.
        /// </summary>
        /// <param name="processIds">The Amuse process ids.</param>
        private Dictionary<string, GPUMemory> GetProcessMemoryUsageGPU(HashSet<int> processIds)
        {
            var gpuMemory = new Dictionary<string, GPUMemory>();
            try
            {
                var dedicatedUsage = SumProcessUsage(_gpuCounterQuery.GetValues(CounterGPUProcessDedicatedUsage), processIds);
                var sharedUsage = SumProcessUsage(_gpuCounterQuery.GetValues(CounterGPUProcessSharedUsage), processIds);
                foreach (var adapterId in dedicatedUsage.Keys.Union(sharedUsage.Keys))
                {
                    dedicatedUsage.TryGetValue(adapterId, out var dedicated);
                    sharedUsage.TryGetValue(adapterId, out var shared);
                    gpuMemory[adapterId] = new GPUMemory((ulong)shared, (ulong)dedicated);
                }
            }
            catch (Exception)
            {
                // Counters unavailable
            }
            return gpuMemory;
        }


        /// <summary>
        /// Sums per-process counter values by adapter for the given processes.
        /// </summary>
        /// <param name="values">The counter values keyed by instance, e.g. pid_1234_luid_0x00000000_0x0000D1AB_phys_0.</param>
        /// <param name="processIds">The process ids to include.</param>
        private static Dictionary<string, double> SumProcessUsage(Dictionary<string, double> values, HashSet<int> processIds)
        {
            var results = new Dictionary<string, double>();
            foreach (var value in values)
            {
                var parts = value.Key.Split('_');
                if (parts.Length < 3 || !int.TryParse(parts[1], out var pid) || !processIds.Contains(pid))
                    continue;

                var adapterId = string.Join('_', parts.Skip(2));
                results.TryGetValue(adapterId, out var total);
                results[adapterId] = total + value.Value;
            }
            return results;
        }


        /// <summary>
        /// Gets the utilization of each engine type, summed over all processes, for each adapter.
        /// </summary>
        private Dictionary<string, Dictionary<string, double>> GetUtilizationGPU()
        {
            var gpuUtilization = new Dictionary<string, Dictionary<string, double>>();
            try
            {
                foreach (var value in _gpuCounterQuery.GetValues(CounterGPUEngine))
                {
                    if (value.Value <= 0)
                        continue;

                    var instanceName = value.Key;
                    if (!instanceName.Contains("engtype_Cuda") && !instanceName.Contains("engtype_3D") && !instanceName.Contains("engtype_Graphics") && !instanceName.Contains("engtype_Compute"))
                        continue;

                    var parts = instanceName.Split('_');
                    if (parts.Length < 11)
                        continue;

                    var adapterId = string.Join('_', parts.Skip(2).Take(5));
                    var instance = string.Concat(parts.Skip(10)).Replace(" ", string.Empty);
                    if (!gpuUtilization.TryGetValue(adapterId, out var adapterUtilization))
                    {
                        adapterUtilization = new Dictionary<string, double>();
                        gpuUtilization[adapterId] = adapterUtilization;
                    }

                    adapterUtilization.TryGetValue(instance, out var total);
                    adapterUtilization[instance] = total + value.Value;
                }
            }
            catch (Exception)
            {
                // Counters unavailable
            }
            return gpuUtilization;
        }


        /// <summary>
        /// Gets the usage percentage of an engine type.
        /// </summary>
        /// <param name="utilization">The adapter engine utilization.</param>
        /// <param name="engine">The engine type.</param>
        private static int GetEngineUsage(Dictionary<string, double> utilization, string engine)
        {
            return utilization.TryGetValue(engine, out var usage) ? (int)Math.Round(usage) : 0;
        }


        /// <summary>
        /// Gets the driver information for each adapter.
        /// </summary>
        private DeviceInfo[] GetDeviceInfo()
        {
            try
            {
                var versions = new List<DeviceInfo>();
                var devices = _adapters.Select(x => x.Description).ToHashSet();
                using (var objectSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT DeviceID, DeviceName, DriverVersion, Location FROM Win32_PnPSignedDriver"))
                {
                    using (var results = objectSearcher.Get())
                    {
                        foreach (var result in results)
                        {
                            using (result)
                            {
                                var deviceName = result["DeviceName"]?.ToString() ?? string.Empty;
                                if (!devices.Contains(deviceName))
                                    continue;

                                var deviceType = string.Empty;
                                var deviceIdStr = result["DeviceID"]?.ToString() ?? string.Empty;
                                var location = result["Location"]?.ToString() ?? string.Empty;
                                var driverVersion = result["DriverVersion"]?.ToString() ?? string.Empty;
                                var match = Regex.Match(deviceIdStr, @"DEV_([0-9A-Fa-f]{4})");
                                if (match.Success)
                                    deviceType = GetDeviceType(deviceIdStr, match.Groups[1].Value.ToUpper());

                                versions.Add(new DeviceInfo(deviceName.Trim(), driverVersion, location, deviceType));
                            }
                        }
                    }
                }
                return versions.ToArray();
            }
            catch (Exception)
            {
                return [];
            }
        }


        /// <summary>
        /// Creates the CPU update thread.
        /// </summary>
        /// <param name="refreshRate">The refresh rate in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private void CreateUpdateThreadCPU(int refreshRate, CancellationToken cancellationToken)
        {
            _cpuUpdateThread = new Thread(() =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _updateThreadResetEvent.WaitOne();
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        _cpuStatus = CreateStatusCPU();
                        if (cancellationToken.WaitHandle.WaitOne(refreshRate))
                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Service disposed
                }
            });
            _cpuUpdateThread.Name = "HardwareService CPU";
            _cpuUpdateThread.IsBackground = true;
            _cpuUpdateThread.Start();
        }


        /// <summary>
        /// Creates the GPU and NPU update thread.
        /// </summary>
        /// <param name="refreshRate">The refresh rate in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private void CreateUpdateThreadGPU(int refreshRate, CancellationToken cancellationToken)
        {
            _gpuUpdateThread = new Thread(() =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _updateThreadResetEvent.WaitOne();
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        if (_gpuCounterQuery.Collect())
                        {
                            var gpuUtilization = GetUtilizationGPU();
                            var gpuMemoryUsage = GetMemoryUsageGPU();
                            var gpuProcessMemoryUsage = GetProcessMemoryUsageGPU(_processIds);
                            _gpuStatus = CreateStatusGPU(gpuUtilization, gpuMemoryUsage, gpuProcessMemoryUsage);
                            _npuStatus = CreateStatusNPU(gpuUtilization, gpuMemoryUsage, gpuProcessMemoryUsage);
                        }

                        if (cancellationToken.WaitHandle.WaitOne(refreshRate))
                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Service disposed
                }
            });
            _gpuUpdateThread.Name = "HardwareService GPU";
            _gpuUpdateThread.IsBackground = true;
            _gpuUpdateThread.Start();
        }


        /// <summary>
        /// Gets the ready fixed and removable disks.
        /// </summary>
        public IReadOnlyList<DiskDevice> GetDisks()
        {
            var disks = new List<DiskDevice>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable))
                        continue;

                    disks.Add(new DiskDevice(drive.Name, drive.VolumeLabel, drive.DriveFormat, drive.DriveType.ToString(), drive.TotalSize / 1024f / 1024f, drive.AvailableFreeSpace / 1024f / 1024f));
                }
                catch (IOException)
                {
                    // Drive became unavailable
                }
                catch (UnauthorizedAccessException)
                {
                    // Drive not accessible
                }
            }
            return disks;
        }


        /// <summary>
        /// Pauses the status updates.
        /// </summary>
        public void Pause()
        {
            if (!_isDisposed)
                _updateThreadResetEvent.Reset();
        }


        /// <summary>
        /// Resumes the status updates.
        /// </summary>
        public void Resume()
        {
            if (!_isDisposed)
                _updateThreadResetEvent.Set();
        }


        /// <summary>
        /// Stops the update threads and releases the counters.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cancellationTokenSource.Cancel();
            _updateThreadResetEvent.Set();
            var isCPUThreadStopped = _cpuUpdateThread.Join(_shutdownTimeout);
            var isGPUThreadStopped = _gpuUpdateThread.Join(_shutdownTimeout);
            if (!isCPUThreadStopped || !isGPUThreadStopped)
                return;

            _gpuCounterQuery.Dispose();
            _updateThreadResetEvent.Dispose();
            _cancellationTokenSource.Dispose();
        }


        /// <summary>
        /// Gets the AMD gfx target for a device, or the device id for other vendors.
        /// </summary>
        /// <param name="device">The PnP device id string.</param>
        /// <param name="deviceId">The 4 digit hex device id.</param>
        public static string GetDeviceType(string device, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(device))
                return "Unknown";

            // Immediate exit if the card is Nvidia, Intel, or virtualized
            if (!device.Contains("VEN_1002"))
                return deviceId;

            // Convert to hex integer for math-based boundary checking
            if (!int.TryParse(deviceId, System.Globalization.NumberStyles.HexNumber, null, out int id))
                return deviceId;

            // =================================================================
            // 1. RDNA 4 GENERATION (Navi 4x - RX 9000 / AI PRO Series)
            // =================================================================
            if (id >= 0x7480 && id <= 0x749F)
            {
                // Deterministic hardware line division: Lower range is Navi 48, Upper is Navi 44
                return (id >= 0x7480 && id <= 0x748F) ? "gfx1201" : "gfx1200";
            }
            if (id == 0x7551) return "gfx1201"; // Dedicated AI PRO Workstation Block

            // =================================================================
            // 2. RDNA 3 / 3.5 GENERATION (RX 7000 / Pro W7000 / Ryzen AI)
            // =================================================================
            if (id >= 0x7440 && id <= 0x745F) return "gfx1100"; // Navi 31 Silicon Family
            if (id >= 0x7460 && id <= 0x746F) return "gfx1101"; // Navi 32 Silicon Family
            if (id >= 0x7470 && id <= 0x747F) return "gfx1102"; // Navi 33 Silicon Family
            if (id == 0x7483) return "gfx1102";                 // Specific Navi 33 Mobile Routing

            if (id == 0x7487 || id == 0x15BF || id == 0x195E) return "gfx1103"; // Phoenix/Hawk Point

            // RDNA 3.5 Mobile APUs (Strix Point - Radeon 890M/880M)
            if (id >= 0x74A0 && id <= 0x74A2) return "gfx1150";
            if (id == 0x150E) return "gfx1150";

            // RDNA 3.5 Heavy Workstation APUs (Strix Halo - Radeon 8000S)
            if (id == 0x74A3) return "gfx1151";
            if (id >= 0x74B0 && id <= 0x74BF) return "gfx1151";

            // =================================================================
            // 3. RDNA 2 GENERATION (RX 6000 / Pro W6000 / Desktop Zen iGPUs)
            // =================================================================
            if (id >= 0x73A0 && id <= 0x73C2) return "gfx1030"; // Navi 21 Silicon Family
            if (id == 0x73C3) return "gfx1031";                 // Specific Server/Mac Routing
            if (id >= 0x73D0 && id <= 0x73EF) return "gfx1031"; // Navi 22 Silicon Family

            if (id >= 0x7400 && id <= 0x741F)
            {
                // 0x7401 and 0x7408 are Rembrandt laptop APUs (Radeon 680M)
                return (id == 0x7401 || id == 0x7408) ? "gfx1035" : "gfx1032";
            }

            if (id >= 0x7420 && id <= 0x743F) return "gfx1034"; // Navi 24 Silicon Family
            if (id == 0x73FF) return "gfx1034";                 // Standalone Navi 24 Exception
            if (id == 0x164E || id == 0x13C0) return "gfx1036"; // Ryzen Desktop Core iGPUs (Zen 4/5)

            // =================================================================
            // 4. RDNA 1 GENERATION (RX 5000 Series / Base Navi)
            // =================================================================
            if (id >= 0x7310 && id <= 0x731F) return "gfx1010"; // Navi 10 Family
            if (id >= 0x7340 && id <= 0x735F) return "gfx1012"; // Navi 14 Family

            // =================================================================
            // 5. CDNA ACCELERATOR TIER (Data Center Instinct Servers)
            // =================================================================
            if (id >= 0x7380 && id <= 0x738F) return "gfx908"; // Instinct MI100
            if (id == 0x740C || id == 0x740F) return "gfx90a"; // Instinct MI200 Series
            if (id == 0x7410) return "gfx90a";
            if (id == 0x7400 || id == 0x740A) return "gfx942"; // Instinct MI300 Series
            if (id == 0x74A5 || id == 0x74A8) return "gfx950"; // Instinct MI350/MI355 Series

            // =================================================================
            // 6. LEGACY GCN & VEGA ARCHITECTURES
            // =================================================================
            if (id >= 0x6860 && id <= 0x687F) return "gfx900"; // Vega 10 
            if (id >= 0x66A0 && id <= 0x66AF) return "gfx906"; // Vega 20
            if (id >= 0x67DF && id <= 0x67FF) return "gfx803"; // Polaris 10/11/20
            if (id == 0x6FDF) return "gfx803";                 // Polaris Embedded

            // IUnknown AMD device
            return deviceId;
        }


        private record struct GPUMemory(ulong SharedUsage, ulong DedicatedUsage);
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
        IReadOnlyList<DiskDevice> GetDisks();
    }


    /// <summary>
    /// DiskDevice structure, sizes in MB.
    /// </summary>
    public record DiskDevice(string Name, string Label, string Format, string DriveType, float SizeTotal, float SizeAvailable);



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
        public float ProcessMemoryUsage { get; set; }
    }

    public record struct DeviceInfo(string Name, string DriverVersion, string Location, string DeviceType);

    public record GPUDevice
    {
        public GPUDevice(AdapterInfo adapter, DeviceInfo deviceInfo)
        {
            Id = (int)adapter.Id;
            AdapterInfo = adapter;
            Name = adapter.Description;
            DeviceType = deviceInfo.DeviceType;
            Location = deviceInfo.Location;
            PCIBusId = GetBusId(deviceInfo.Location);
            HardwareLUID = GetLuid(adapter);
            DriverVersion = deviceInfo.DriverVersion;
            MemoryTotal = adapter.DedicatedVideoMemory / 1024f / 1024f;
            SharedMemoryTotal = adapter.SharedSystemMemory / 1024f / 1024f;
            AdapterId = $"luid_0x{adapter.AdapterLuid.HighPart:X8}_0x{adapter.AdapterLuid.LowPart:X8}_phys_0";
        }

        public int Id { get; }
        public string Name { get; }
        public string DeviceType { get; }
        public string Location { get; }
        public int PCIBusId { get; }
        public long HardwareLUID { get; }
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

        private static long GetLuid(AdapterInfo adapter)
        {
            try
            {
                uint high = Convert.ToUInt32(adapter.AdapterLuid.HighPart);
                uint low = Convert.ToUInt32(adapter.AdapterLuid.LowPart);
                return ((long)high << 32) | low;
            }
            catch
            {
                return 0;
            }
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

        public float ProcessMemoryUsage { get; set; }
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
            DeviceType = deviceInfo.DeviceType;
            DriverVersion = deviceInfo.DriverVersion;
            MemoryTotal = adapter.SharedSystemMemory / 1024f / 1024f;
            AdapterId = $"luid_0x{adapter.AdapterLuid.HighPart:X8}_0x{adapter.AdapterLuid.LowPart:X8}_phys_0";
        }

        public int Id { get; }
        public string Name { get; }
        public string DeviceType { get; }
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

        public float ProcessMemoryUsage { get; set; }
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


        /// <summary>
        /// Gets the system idle, kernel and user times.
        /// </summary>
        public static CPUTimes GetCPUTimes()
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return default;

            return new CPUTimes(idle, kernel, user);
        }


        /// <summary>
        /// Gets a process and all of its descendant processes, e.g. Amuse and its AmuseHost processes.
        /// </summary>
        /// <param name="rootProcessId">The root process id.</param>
        public static HashSet<int> GetProcessTree(int rootProcessId)
        {
            var processTree = new HashSet<int> { rootProcessId };
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == INVALID_HANDLE_VALUE)
                return processTree;

            try
            {
                var processes = new List<(int ProcessId, int ParentId)>();
                var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
                if (Process32FirstW(snapshot, ref entry))
                {
                    do
                    {
                        processes.Add(((int)entry.ProcessId, (int)entry.ParentProcessId));
                    }
                    while (Process32NextW(snapshot, ref entry));
                }

                var isChanged = true;
                while (isChanged)
                {
                    isChanged = false;
                    foreach (var (processId, parentId) in processes)
                    {
                        if (processId != parentId && processTree.Contains(parentId) && processTree.Add(processId))
                            isChanged = true;
                    }
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return processTree;
        }


        /// <summary>
        /// Gets the total working set of the given processes, in bytes.
        /// </summary>
        /// <param name="processIds">The process ids.</param>
        public static ulong GetWorkingSet(IEnumerable<int> processIds)
        {
            ulong workingSet = 0;
            foreach (var processId in processIds)
            {
                var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (process == IntPtr.Zero)
                    continue;

                try
                {
                    var counters = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
                    if (K32GetProcessMemoryInfo(process, ref counters, counters.Size))
                        workingSet += (ulong)counters.WorkingSetSize;
                }
                finally
                {
                    CloseHandle(process);
                }
            }
            return workingSet;
        }


        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool K32GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Size;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
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
        [Display(Name = "AdapterFlags0", ResourceType = typeof(AppEnums))]
        None = 0,

        [Display(Name = "AdapterFlags1", ResourceType = typeof(AppEnums))]
        Remote = 1,

        [Display(Name = "AdapterFlags2", ResourceType = typeof(AppEnums))]
        Software = 2
    }

    public enum AdapterType : uint
    {
        [Display(Name = "AdapterType0", ResourceType = typeof(AppEnums))]
        GPU = 0,

        [Display(Name = "AdapterType1", ResourceType = typeof(AppEnums))]
        NPU = 1,

        [Display(Name = "AdapterType2", ResourceType = typeof(AppEnums))]
        Other = 2
    }

    /// <summary>
    /// System CPU times in 100ns units, kernel time includes idle time.
    /// </summary>
    public record struct CPUTimes(ulong Idle, ulong Kernel, ulong User);


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


    /// <summary>
    /// Reads wildcard Windows performance counters through PDH, which is far cheaper than the WMI formatted performance classes.
    /// </summary>
    public sealed class PerformanceCounterQuery : IDisposable
    {
        private const uint PDH_FMT_DOUBLE = 0x00000200;
        private const uint PDH_FMT_NOCAP100 = 0x00008000;
        private const uint PDH_MORE_DATA = 0x800007D2;
        private const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
        private const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

        private readonly Dictionary<string, IntPtr> _counters = new Dictionary<string, IntPtr>();
        private IntPtr _query;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceCounterQuery"/> class.
        /// </summary>
        /// <param name="counterPaths">The English counter paths, e.g. \GPU Engine(*)\Utilization Percentage.</param>
        public PerformanceCounterQuery(params string[] counterPaths)
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0)
            {
                _query = IntPtr.Zero;
                return;
            }

            foreach (var counterPath in counterPaths)
            {
                if (PdhAddEnglishCounter(_query, counterPath, IntPtr.Zero, out var counter) == 0)
                    _counters[counterPath] = counter;
            }
        }


        /// <summary>
        /// Collects a new sample for every counter in the query.
        /// </summary>
        /// <returns><c>true</c> if the sample was collected, otherwise <c>false</c>.</returns>
        public bool Collect()
        {
            if (_isDisposed || _query == IntPtr.Zero)
                return false;

            return PdhCollectQueryData(_query) == 0;
        }


        /// <summary>
        /// Gets the formatted value of each instance of a counter from the last collected sample.
        /// Rate counters return no values until two samples have been collected.
        /// </summary>
        /// <param name="counterPath">The counter path.</param>
        /// <returns>The value for each instance name.</returns>
        public Dictionary<string, double> GetValues(string counterPath)
        {
            var values = new Dictionary<string, double>();
            if (_isDisposed || !_counters.TryGetValue(counterPath, out var counter))
                return values;

            uint bufferSize = 0;
            uint itemCount = 0;
            var status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref bufferSize, ref itemCount, IntPtr.Zero);
            if (status != PDH_MORE_DATA || bufferSize == 0)
                return values;

            var buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref bufferSize, ref itemCount, buffer);
                if (status != 0)
                    return values;

                var itemSize = Marshal.SizeOf<PdhCounterValueItem>();
                for (int i = 0; i < itemCount; i++)
                {
                    var item = Marshal.PtrToStructure<PdhCounterValueItem>(buffer + (i * itemSize));
                    if (item.CStatus != PDH_CSTATUS_VALID_DATA && item.CStatus != PDH_CSTATUS_NEW_DATA)
                        continue;

                    var name = Marshal.PtrToStringUni(item.Name);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    values.TryGetValue(name, out var existing);
                    values[name] = existing + item.Value;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return values;
        }


        /// <summary>
        /// Closes the query and releases its counters.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_query != IntPtr.Zero)
            {
                PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
            _counters.Clear();
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct PdhCounterValueItem
        {
            public IntPtr Name;
            public uint CStatus;
            public double Value;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQuery(string dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr itemBuffer);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);
    }
}
