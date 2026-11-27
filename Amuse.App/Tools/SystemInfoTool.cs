using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TensorStack.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class SystemInfoTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "system_info";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Get information about the local computer: operating system, CPU, RAM, GPU and VRAM, NPU, disk space and battery.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => AppDefault.ToolSystemInfo;

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f2db";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 3;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool for questions about the user's hardware, memory or disk usage, or whether the computer can run a model or application. Memory and disk values are in GB.",
                "parameters": {
                    "type": "object",
                    "properties": {}
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                var hardwareService = App.GetService<IHardwareService>();

                // Operating system
                var osVersion = Environment.OSVersion.Version;
                var osName = osVersion.Major == 10 && osVersion.Build >= 22000 ? "Windows 11" : RuntimeInformation.OSDescription;
                var system = new OperatingSystemInfo(osName, osVersion.ToString(), RuntimeInformation.OSArchitecture.ToString(), Environment.MachineName, TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm"));

                // CPU and memory
                var memory = DeviceInterop.GetMemoryStatus();
                var cpuStatus = hardwareService.CPUStatus;
                var cpu = new CpuInfo(hardwareService.CPUDevice?.Name, Environment.ProcessorCount, cpuStatus.Usage);
                var ram = new MemoryInfo(ToolHelper.ToGB(memory.TotalPhysicalMemory), ToolHelper.ToGB(memory.AvailablePhysicalMemory), (int)memory.MemoryLoad);

                // GPU
                var gpuStatus = hardwareService.GPUStatus;
                var gpus = hardwareService.GPUDevices
                    .Where(device => IsDetectedDevice(settings, device))
                    .Select(device =>
                    {
                        var status = gpuStatus.FirstOrDefault(x => x.Id == device.Id);
                        return new GpuInfo
                        (
                            device.Name,
                            device.DeviceType,
                            device.DriverVersion,
                            MegabytesToGB(device.MemoryTotal),
                            MegabytesToGB(Math.Max(0, device.MemoryTotal - status.MemoryUsage)),
                            MegabytesToGB(device.SharedMemoryTotal),
                            Math.Max(status.UsageCompute, status.UsageGraphics)
                        );
                    })
                    .ToList();

                // NPU
                var npuDevice = hardwareService.NPUDevice;
                var npu = npuDevice == null ? null : new NpuInfo(npuDevice.Name, npuDevice.DriverVersion);

                // Disks
                var disks = hardwareService.GetDisks()
                    .Select(x => new DiskInfo(x.Name, x.Label, x.Format, MegabytesToGB(x.SizeTotal), MegabytesToGB(x.SizeAvailable)))
                    .ToList();

                // Power
                var powerStatus = SystemInformation.PowerStatus;
                var hasBattery = !powerStatus.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) && powerStatus.BatteryChargeStatus != BatteryChargeStatus.Unknown;
                var power = new PowerInfo(powerStatus.PowerLineStatus == PowerLineStatus.Online, hasBattery, hasBattery ? (int)Math.Round(powerStatus.BatteryLifePercent * 100) : null);

                var result = new SystemInfoResult(system, cpu, ram, gpus, npu, disks, power);
                return Task.FromResult(SuccessResult(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorResult(ex));
            }
        }


        /// <summary>
        /// Determines whether the adapter is one of the devices Amuse detected, Windows can list the same graphics card as more than one adapter.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="device">The adapter.</param>
        private static bool IsDetectedDevice(Settings settings, GPUDevice device)
        {
            if (settings.Devices.IsNullOrEmpty())
                return true;

            return settings.Devices.Any(x => x.HardwareLUID == device.HardwareLUID);
        }


        /// <summary>
        /// Converts megabytes to gigabytes.
        /// </summary>
        /// <param name="megabytes">The megabytes.</param>
        private static double MegabytesToGB(float megabytes)
        {
            return Math.Round(megabytes / 1024d, 1);
        }


        /// <summary>
        /// OperatingSystemInfo structure.
        /// </summary>
        private sealed record OperatingSystemInfo(string Name, string Version, string Architecture, string MachineName, string Uptime);


        /// <summary>
        /// CpuInfo structure.
        /// </summary>
        private sealed record CpuInfo(string Name, int LogicalProcessors, int UsagePercent);


        /// <summary>
        /// MemoryInfo structure.
        /// </summary>
        private sealed record MemoryInfo(double TotalGB, double AvailableGB, int UsagePercent);


        /// <summary>
        /// GpuInfo structure.
        /// </summary>
        private sealed record GpuInfo(string Name, string DeviceType, string DriverVersion, double VramTotalGB, double VramAvailableGB, double SharedMemoryGB, int UsagePercent);


        /// <summary>
        /// NpuInfo structure.
        /// </summary>
        private sealed record NpuInfo(string Name, string DriverVersion);


        /// <summary>
        /// DiskInfo structure.
        /// </summary>
        private sealed record DiskInfo(string Name, string Label, string Format, double TotalGB, double FreeGB);


        /// <summary>
        /// PowerInfo structure.
        /// </summary>
        private sealed record PowerInfo(bool IsPluggedIn, bool HasBattery, int? BatteryPercent);


        /// <summary>
        /// SystemInfoResult structure.
        /// </summary>
        private sealed record SystemInfoResult(OperatingSystemInfo System, CpuInfo Cpu, MemoryInfo Ram, List<GpuInfo> Gpus, NpuInfo Npu, List<DiskInfo> Disks, PowerInfo Power);
    }
}
