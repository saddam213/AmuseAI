using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Lists the models that are already loaded before another model is loaded next to them, each can be unloaded from the list.
    /// The memory left on the devices they are loaded on is shown with them, and follows the models that are unloaded.
    /// </summary>
    public sealed partial class ModelLoadedListDialog : DialogControl
    {
        private readonly IHardwareService _hardwareService;
        private readonly DispatcherTimer _updateTimer;
        private bool _isBusy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelLoadedListDialog"/> class.
        /// </summary>
        /// <param name="hardwareService">The hardware service.</param>
        public ModelLoadedListDialog(IHardwareService hardwareService)
        {
            _hardwareService = hardwareService;
            _updateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            UnloadCommand = new AsyncRelayCommand<ModelLoadedItem>(UnloadAsync);
            UnloadAllCommand = new AsyncRelayCommand(UnloadAllAsync);
            ContinueCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            Items = new ObservableCollection<ModelLoadedItem>();
            Devices = new ObservableCollection<ModelLoadedDevice>();
            InitializeComponent();
        }

        public AsyncRelayCommand<ModelLoadedItem> UnloadCommand { get; }
        public AsyncRelayCommand UnloadAllCommand { get; }
        public AsyncRelayCommand ContinueCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<ModelLoadedItem> Items { get; }
        public ObservableCollection<ModelLoadedDevice> Devices { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a model is unloading or the dialog is closing after the unload, the buttons are locked while set.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            set { SetProperty(ref _isBusy, value); }
        }


        /// <summary>
        /// Shows the dialog for the loaded models.
        /// </summary>
        /// <param name="items">The loaded models.</param>
        /// <param name="devices">The devices the new model and the loaded models are on, their memory is shown above the models.</param>
        /// <returns><c>true</c> when the load continues with the models that are still loaded, <c>false</c> when it was canceled.</returns>
        public Task<bool> ShowDialogAsync(IReadOnlyList<ModelLoadedItem> items, IReadOnlyList<DeviceModel> devices)
        {
            Title = AppDefault.ModelLoadedTitle;
            foreach (var item in items)
                Items.Add(item);

            foreach (var device in devices)
                Devices.Add(new ModelLoadedDevice(device));

            UpdateDevices();
            _updateTimer.Start();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Unloads a loaded model.
        /// </summary>
        /// <param name="item">The loaded model.</param>
        private Task UnloadAsync(ModelLoadedItem item)
        {
            return UnloadItemsAsync([item]);
        }


        /// <summary>
        /// Unloads every model that is still loaded.
        /// </summary>
        private Task UnloadAllAsync()
        {
            return UnloadItemsAsync(Items.Where(x => x.Status == ModelLoadedStatus.Loaded).ToList());
        }


        /// <summary>
        /// Unloads the models one after the other, the load continues once none is left loaded and is canceled when an unload fails.
        /// </summary>
        /// <param name="items">The loaded models to unload.</param>
        private async Task UnloadItemsAsync(IReadOnlyList<ModelLoadedItem> items)
        {
            var isUnloaded = true;
            var isClosing = false;
            IsBusy = true;
            try
            {
                foreach (var item in items)
                {
                    isUnloaded = await item.UnloadAsync();
                    if (!isUnloaded)
                        break;
                }

                isClosing = !isUnloaded || Items.All(x => x.Status == ModelLoadedStatus.Unloaded);
            }
            finally
            {
                // The buttons stay locked while the dialog closes
                IsBusy = isClosing;
            }

            // Result
            if (!isUnloaded)
                await base.CancelAsync();
            else if (isClosing)
                await base.SaveAsync();
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
        /// Updates the memory in use on the devices from the latest hardware status.
        /// </summary>
        private void UpdateDevices()
        {
            try
            {
                foreach (var gpuStatus in _hardwareService.GPUStatus)
                {
                    var device = Devices.FirstOrDefault(x => x.Device.Id == gpuStatus.Id && x.Device.Type == DeviceType.GPU);
                    if (device != null)
                        device.MemoryUsage = gpuStatus.MemoryUsage / 1024f;
                }
            }
            catch (Exception)
            {
                // Hardware status unavailable, retry next tick
            }
        }


        /// <summary>
        /// Keeps the dialog open while a model is unloading, the load is only canceled or continued once the unload is done.
        /// </summary>
        /// <param name="e">The <see cref="CancelEventArgs"/> instance containing the event data.</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (Items.Any(x => x.Status == ModelLoadedStatus.Unloading))
                e.Cancel = true;

            base.OnClosing(e);
        }


        /// <summary>
        /// Stops the device updates when the dialog has closed.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            _updateTimer.Stop();
            base.OnClosed(e);
        }
    }
}
