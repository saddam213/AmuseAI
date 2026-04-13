using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.Providers;
using TensorStack.WPF;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for SystemInfoControl.xaml
    /// </summary>
    public partial class SystemInfoControl : UserControl
    {
        private readonly IHardwareService _hardwareService;
        private readonly DispatcherTimer _updateTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemInfoControl"/> class.
        /// </summary>
        public SystemInfoControl()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _hardwareService = App.GetService<IHardwareService>();

                var devices = Provider.GetDevices()
                    .Where(x => !string.IsNullOrEmpty(x.HardwareVendor))
                    .Select(x => new DeviceInfo
                    {
                        Id = x.Id,
                        DeviceId = x.Id,
                        Name = x.Name,
                        HardwareLUID = x.HardwareLUID,
                        HardwareID = x.HardwareID,
                        HardwareVendor = x.HardwareVendor,
                        HardwareVendorId = x.HardwareVendorId,
                        Memory = x.Memory,
                        Type = x.Type
                    });

                DeviceCollection = [.. devices];
                _updateTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Normal, UpdateDevices, Dispatcher);
                _updateTimer.Start();
                _ = Task.Run(UpdateDevices);
            }

            InitializeComponent();
        }


        public List<DeviceInfo> DeviceCollection { get; }


        private void UpdateDevices(object sender, EventArgs e)
        {
            foreach (var device in DeviceCollection)
            {
                device.NotifyPropertyChanged(nameof(DeviceInfo.Usage));
                device.NotifyPropertyChanged(nameof(DeviceInfo.MemoryUsage));
                device.NotifyPropertyChanged(nameof(DeviceInfo.MemoryRemaining));
                device.NotifyPropertyChanged(nameof(DeviceInfo.ProcessMemoryUsage));
                device.NotifyPropertyChanged(nameof(DeviceInfo.ProgressValue));
                device.NotifyPropertyChanged(nameof(DeviceInfo.ProgressSubValue));
            }
        }


        private async Task UpdateDevices()
        {
            var process = Process.GetCurrentProcess();
            while (!process.HasExited)
            {
                try
                {
                    process.Refresh(); // Update stats
                    var cpuStatus = _hardwareService.CPUStatus;
                    var cpuUpdate = DeviceCollection.FirstOrDefault(x => x.Type == DeviceType.CPU);
                    if (cpuUpdate != null)
                    {
                        var memoryUsage = (cpuStatus.MemoryTotal - cpuStatus.MemoryAvailable);
                        var processMemoryUsage = process.WorkingSet64 / 1024f / 1024f;
                        cpuUpdate.Memory = _hardwareService.CPUDevice.MemoryTotal;
                        cpuUpdate.Usage = cpuStatus.Usage;
                        cpuUpdate.MemoryUsage = memoryUsage / 1024f;
                        cpuUpdate.ProcessMemoryUsage = processMemoryUsage / 1024f;
                    }

                    var npuStatus = _hardwareService.NPUStatus;
                    var npuUpdate = DeviceCollection.FirstOrDefault(x => x.Type == DeviceType.NPU);
                    if (npuUpdate != null)
                    {
                        npuUpdate.Memory = _hardwareService.NPUDevice.MemoryTotal;
                        npuUpdate.Usage = npuStatus.Usage;
                        npuUpdate.MemoryUsage = npuStatus.MemoryUsage / 1024f;
                    }

                    foreach (var gpuStatus in _hardwareService.GPUStatus)
                    {
                        var gpuUpdate = DeviceCollection.FirstOrDefault(x => x.DeviceId == gpuStatus.Id && x.Type == DeviceType.GPU);
                        if (gpuUpdate == null)
                            continue;

                        gpuUpdate.Usage = GetGPUUsage(gpuStatus);
                        gpuUpdate.MemoryUsage = gpuStatus.MemoryUsage / 1024f;
                        gpuUpdate.ProcessMemoryUsage = gpuStatus.ProcessMemoryTotal / 1024f;
                    }
                }
                catch (Exception) { }
                await Task.Delay(250); // Non-blocking delay
            }
        }


        /// <summary>
        /// Gets the GPU usage.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>System.Double.</returns>
        private int GetGPUUsage(GPUStatus status)
        {
            var compute = Math.Max(status.UsageCompute, status.UsageCompute1);
            var graphics = Math.Max(status.UsageGraphics, status.UsageGraphics1);
            var usage = Math.Max(status.UsageCuda, Math.Max(compute, graphics));
            return Math.Min(usage, 100);
        }

    }

    public class DeviceInfo : BaseModel
    {
        private double _memory;

        public int Id { get; init; }
        public int DeviceId { get; init; }
        public string Name { get; init; }
        public DeviceType Type { get; init; }
        public int HardwareID { get; init; }
        public int HardwareLUID { get; init; }
        public int HardwareVendorId { get; init; }
        public string HardwareVendor { get; init; }
        public int Usage { get; set; }
        public double MemoryUsage { get; set; }
        public double ProcessMemoryUsage { get; set; }
        public double MemoryRemaining => MemoryGB - MemoryUsage;
        public double ProgressValue => MemoryUsage;
        public double ProgressSubValue => MemoryUsage - ProcessMemoryUsage;

        public double Memory
        {
            get { return _memory; }
            set
            {
                _memory = value;
                MemoryGB = (int)Math.Round(_memory / 1024.0, 0, MidpointRounding.ToEven);
            }
        }
        public int MemoryGB { get; set; }
    }

}
