using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.OnnxRuntime;
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
            DeviceCollection = new List<DeviceInfo>();
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _hardwareService = App.GetService<IHardwareService>();

                var cpuDevice = Provider.GetDevice(DeviceType.CPU);
                DeviceCollection.Add(new DeviceInfo
                {
                    Id = cpuDevice.Id,
                    DeviceId = cpuDevice.Id,
                    Name = cpuDevice.Name,
                    HardwareLUID = 0,
                    HardwareID = cpuDevice.HardwareID,
                    HardwareVendorId = cpuDevice.HardwareVendorId,
                    Memory = _hardwareService.CPUDevice?.MemoryTotal ?? cpuDevice.Memory,
                    Type = cpuDevice.Type
                });

                DeviceCollection.AddRange(_hardwareService.GetGPUDevices().Select(x => new DeviceInfo
                {
                    Id = x.Id,
                    DeviceId = x.Id,
                    Name = x.Name,
                    HardwareLUID = x.HardwareLUID,
                    HardwareID = x.HardwareID,
                    HardwareVendorId = x.HardwareVendorId,
                    Memory = x.Memory,
                    Type = x.Type
                }));

                _updateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _updateTimer.Tick += UpdateTimer_Tick;
                Loaded += SystemInfoControl_Loaded;
                Unloaded += SystemInfoControl_Unloaded;
            }

            InitializeComponent();
        }


        public List<DeviceInfo> DeviceCollection { get; }


        /// <summary>
        /// Starts the updates when the control is loaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SystemInfoControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDevices();
            _updateTimer.Start();
        }


        /// <summary>
        /// Stops the updates when the control is unloaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SystemInfoControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _updateTimer.Stop();
        }


        /// <summary>
        /// Handles the update timer tick.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateDevices();
        }


        /// <summary>
        /// Updates the devices from the latest hardware status.
        /// </summary>
        private void UpdateDevices()
        {
            try
            {
                foreach (var device in DeviceCollection)
                {
                    if (device.Type == DeviceType.CPU)
                        UpdateDeviceCPU(device, _hardwareService.CPUStatus);
                    else if (device.Type == DeviceType.NPU)
                        UpdateDeviceNPU(device, _hardwareService.NPUStatus);
                }

                foreach (var gpuStatus in _hardwareService.GPUStatus)
                {
                    var gpuDevice = DeviceCollection.FirstOrDefault(x => x.DeviceId == gpuStatus.Id && x.Type == DeviceType.GPU);
                    if (gpuDevice != null)
                        UpdateDeviceGPU(gpuDevice, gpuStatus);
                }
            }
            catch (Exception)
            {
                // Hardware status unavailable, retry next tick
            }
        }


        /// <summary>
        /// Updates the CPU device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="status">The status.</param>
        private static void UpdateDeviceCPU(DeviceInfo device, CPUStatus status)
        {
            if (status.MemoryTotal <= 0)
                return;

            device.Memory = status.MemoryTotal;
            device.Usage = status.Usage;
            device.MemoryUsage = (status.MemoryTotal - status.MemoryAvailable) / 1024f;
            device.ProcessMemoryUsage = status.ProcessMemoryUsage / 1024f;
        }


        /// <summary>
        /// Updates the NPU device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="status">The status.</param>
        private static void UpdateDeviceNPU(DeviceInfo device, NPUStatus status)
        {
            if (status.MemoryTotal <= 0)
                return;

            device.Memory = status.MemoryTotal;
            device.Usage = status.Usage;
            device.MemoryUsage = status.MemoryUsage / 1024f;
            device.ProcessMemoryUsage = status.ProcessSharedMemoryUsage / 1024f;
        }


        /// <summary>
        /// Updates the GPU device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="status">The status.</param>
        private static void UpdateDeviceGPU(DeviceInfo device, GPUStatus status)
        {
            device.Usage = GetGPUUsage(status);
            device.MemoryUsage = status.MemoryUsage / 1024f;
            device.ProcessMemoryUsage = status.ProcessMemoryUsage / 1024f;
        }


        /// <summary>
        /// Gets the GPU usage.
        /// </summary>
        /// <param name="status">The status.</param>
        private static int GetGPUUsage(GPUStatus status)
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
        private int _memoryGB;
        private int _usage;
        private double _memoryUsage;
        private double _processMemoryUsage;

        public int Id { get; init; }
        public int DeviceId { get; init; }
        public string Name { get; init; }
        public DeviceType Type { get; init; }
        public int HardwareID { get; init; }
        public long HardwareLUID { get; init; }
        public int HardwareVendorId { get; init; }

        public int Usage
        {
            get { return _usage; }
            set { SetProperty(ref _usage, value); }
        }

        public double MemoryUsage
        {
            get { return _memoryUsage; }
            set
            {
                if (SetProperty(ref _memoryUsage, value))
                    NotifyMemoryChanged();
            }
        }

        public double ProcessMemoryUsage
        {
            get { return _processMemoryUsage; }
            set
            {
                if (SetProperty(ref _processMemoryUsage, value))
                    NotifyMemoryChanged();
            }
        }

        public double Memory
        {
            get { return _memory; }
            set
            {
                if (SetProperty(ref _memory, value))
                    MemoryGB = (int)Math.Round(_memory / 1024.0, 0, MidpointRounding.ToEven);
            }
        }

        public int MemoryGB
        {
            get { return _memoryGB; }
            private set
            {
                if (SetProperty(ref _memoryGB, value))
                    NotifyMemoryChanged();
            }
        }

        public double MemoryRemaining => Math.Max(0, MemoryGB - MemoryUsage);
        public double ProgressValue => MemoryUsage;
        public double ProgressSubValue => Math.Max(0, MemoryUsage - ProcessMemoryUsage);


        /// <summary>
        /// Notifies the computed memory properties.
        /// </summary>
        private void NotifyMemoryChanged()
        {
            NotifyPropertyChanged(nameof(MemoryRemaining));
            NotifyPropertyChanged(nameof(ProgressValue));
            NotifyPropertyChanged(nameof(ProgressSubValue));
        }
    }
}
