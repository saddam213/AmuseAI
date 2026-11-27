using System;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// A loaded model in the list of loaded models, with the view that loaded it, the memory it uses and how to unload it.
    /// </summary>
    public sealed class ModelLoadedItem : BaseModel
    {
        private readonly Func<Task<bool>> _unloadAsync;
        private ModelLoadedStatus _status;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelLoadedItem"/> class.
        /// </summary>
        /// <param name="name">The name of the loaded model.</param>
        /// <param name="viewName">The name of the view that loaded the model, or <c>null</c> when it is not known.</param>
        /// <param name="deviceName">The name of the device the model is loaded on, or <c>null</c> when it is not known or every model is on the same device.</param>
        /// <param name="note">What loading the new model replaces of the model, or <c>null</c> when it only stays loaded next to it.</param>
        /// <param name="memorySize">The memory the model needs in GB, with the upscaler and extractor unloaded with it, zero when it is not known.</param>
        /// <param name="unloadAsync">The unload operation, <c>false</c> when the model could not be unloaded.</param>
        public ModelLoadedItem(string name, string viewName, string deviceName, string note, float memorySize, Func<Task<bool>> unloadAsync)
        {
            Name = name;
            Detail = string.Join(" · ", new[] { viewName, deviceName }.Where(x => !string.IsNullOrEmpty(x)));
            Note = note;
            MemorySize = memorySize;
            _unloadAsync = unloadAsync;
        }

        public string Name { get; }
        public string Detail { get; }
        public string Note { get; }
        public float MemorySize { get; }

        public ModelLoadedStatus Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value); }
        }


        /// <summary>
        /// Unloads the model, it is shown as loaded again when the unload fails.
        /// </summary>
        /// <returns><c>true</c> when the model was unloaded.</returns>
        public async Task<bool> UnloadAsync()
        {
            var isUnloaded = false;
            Status = ModelLoadedStatus.Unloading;
            try
            {
                isUnloaded = await _unloadAsync();
                return isUnloaded;
            }
            finally
            {
                Status = isUnloaded ? ModelLoadedStatus.Unloaded : ModelLoadedStatus.Loaded;
            }
        }
    }


    /// <summary>
    /// A device in the list of loaded models, with its memory and how much of it is in use.
    /// </summary>
    public sealed class ModelLoadedDevice : BaseModel
    {
        private double _memoryUsage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelLoadedDevice"/> class.
        /// </summary>
        /// <param name="device">The device.</param>
        public ModelLoadedDevice(DeviceModel device)
        {
            Device = device;
        }

        public DeviceModel Device { get; }
        public string Name => Device.Name;
        public int MemoryGB => Device.MemoryGB;
        public double MemoryRemaining => Math.Max(0, MemoryGB - MemoryUsage);

        public double MemoryUsage
        {
            get { return _memoryUsage; }
            set { SetProperty(ref _memoryUsage, value); NotifyPropertyChanged(nameof(MemoryRemaining)); }
        }
    }


    public enum ModelLoadedStatus
    {
        Loaded = 0,
        Unloading = 1,
        Unloaded = 2
    }
}
