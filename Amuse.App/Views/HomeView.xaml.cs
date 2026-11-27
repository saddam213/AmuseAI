using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for HomeView.xaml
    /// </summary>
    public partial class HomeView : ViewBase
    {
        private const int ModelCount = 6;
        private readonly IHardwareService _hardwareService;
        private readonly IEnvironmentService _environmentService;
        private string _cpuName;
        private string _npuName;
        private int _systemMemory;
        private bool _isAppSetupShown;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="hardwareService">The hardware service.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="logger">The logger.</param>
        public HomeView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IHardwareService hardwareService, IEnvironmentService environmentService, ILogger<HomeView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            _hardwareService = hardwareService;
            _environmentService = environmentService;
            Backends = [BackendType.OnnxRuntime, BackendType.AudioCpp, BackendType.LlamaCpp, BackendType.StableDiffusionCpp];
            Devices = new ObservableCollection<HomeDeviceItem>();
            NewModels = new ObservableCollection<IGenerateModel>();
            RecommendedModels = new ObservableCollection<IGenerateModel>();
            RecentModels = new ObservableCollection<IGenerateModel>();
            NavigateCommand = new AsyncRelayCommand<View>(NavigateAsync);
            OpenModelCommand = new AsyncRelayCommand<IGenerateModel>(OpenModelAsync);
            ManageEnvironmentsCommand = new AsyncRelayCommand(ManageEnvironmentsAsync);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.Home;

        /// <summary>
        /// Gets the application version.
        /// </summary>
        public string AppVersion => App.AppVersionDisplay;

        /// <summary>
        /// Gets the backends shown in the device table.
        /// </summary>
        public BackendType[] Backends { get; }

        /// <summary>
        /// Gets the devices and the state of each backend.
        /// </summary>
        public ObservableCollection<HomeDeviceItem> Devices { get; }

        /// <summary>
        /// Gets the new models.
        /// </summary>
        public ObservableCollection<IGenerateModel> NewModels { get; }

        /// <summary>
        /// Gets the recommended models.
        /// </summary>
        public ObservableCollection<IGenerateModel> RecommendedModels { get; }

        /// <summary>
        /// Gets the recently used models.
        /// </summary>
        public ObservableCollection<IGenerateModel> RecentModels { get; }

        /// <summary>
        /// Gets the navigate command.
        /// </summary>
        public AsyncRelayCommand<View> NavigateCommand { get; }

        /// <summary>
        /// Gets the open model command.
        /// </summary>
        public AsyncRelayCommand<IGenerateModel> OpenModelCommand { get; }

        /// <summary>
        /// Gets the manage environments command, opens the app setup dialog with every backend.
        /// </summary>
        public AsyncRelayCommand ManageEnvironmentsCommand { get; }

        /// <summary>
        /// Gets or sets the name of the CPU.
        /// </summary>
        public string CPUName
        {
            get { return _cpuName; }
            set { SetProperty(ref _cpuName, value); }
        }

        /// <summary>
        /// Gets or sets the name of the NPU, if the system has one.
        /// </summary>
        public string NPUName
        {
            get { return _npuName; }
            set { SetProperty(ref _npuName, value); }
        }

        /// <summary>
        /// Gets or sets the system memory in GB.
        /// </summary>
        public int SystemMemory
        {
            get { return _systemMemory; }
            set { SetProperty(ref _systemMemory, value); }
        }


        /// <summary>
        /// On View Open
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            try
            {
                RefreshDevices();
                RefreshModels();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Home] [Open] An exception occurred refreshing the home view.");
            }

            // Disclaimer and App Setup, once per run. The dialogs are modal and block while open,
            // so queue them to show after this view has opened and rendered
            if (!_isAppSetupShown)
            {
                _isAppSetupShown = true;
                Dispatcher.InvokeAsync(() => AppStartupAsync(), DispatcherPriority.ApplicationIdle);
            }
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Refreshes the devices and backend states.
        /// </summary>
        private void RefreshDevices()
        {
            // System
            CPUName = _hardwareService.CPUDevice?.Name;
            SystemMemory = (int)Math.Round((_hardwareService.CPUDevice?.MemoryTotal ?? 0) / 1024f);
            NPUName = _hardwareService.NPUDevice?.Name;

            // Devices
            Devices.Clear();
            foreach (var device in Settings.Devices ?? [])
            {
                var backends = Backends
                    .Select(backend => GetBackendItem(device, backend))
                    .ToArray();
                Devices.Add(new HomeDeviceItem(device, backends));
            }
        }


        /// <summary>
        /// Gets the state of the backend on the device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="backend">The backend.</param>
        private HomeBackendItem GetBackendItem(DeviceModel device, BackendType backend)
        {
            if (device.SupportedBackends is null || !device.SupportedBackends.Contains(backend))
                return new HomeBackendItem(backend, HomeBackendStatus.Unsupported);

            // OnnxRuntime runs in process, no environment required
            if (backend == BackendType.OnnxRuntime)
                return new HomeBackendItem(backend, HomeBackendStatus.Ready);

            var environments = Settings.Environments
                .Where(x => x.Backend == backend && x.Vendor == device.Vendor)
                .Where(x => x.Type != EnvironmentType.Device || x.Device == device.HardwareID)
                .ToList();
            if (environments.Count == 0)
                return new HomeBackendItem(backend, HomeBackendStatus.Unsupported);

            return environments.Any(_environmentService.Exists)
                ? new HomeBackendItem(backend, HomeBackendStatus.Ready)
                : new HomeBackendItem(backend, HomeBackendStatus.NotInstalled);
        }


        /// <summary>
        /// Shows the disclaimer until it is accepted, then the app setup dialog.
        /// </summary>
        private async Task AppStartupAsync()
        {
            if (!await AppDisclaimerAsync())
                return;

            await AppSetupAsync();
        }


        /// <summary>
        /// Shows the disclaimer until it is accepted, exiting the application if it is declined.
        /// </summary>
        /// <returns><c>true</c> if the disclaimer has been accepted, otherwise <c>false</c>.</returns>
        private async Task<bool> AppDisclaimerAsync()
        {
            if (Settings.IsDisclaimerAccepted)
                return true;

            try
            {
                Logger.LogInformation("[Home] [AppDisclaimer] Showing disclaimer...");
                var disclaimerDialog = DialogService.GetDialog<AppDisclaimerDialog>();
                if (!await disclaimerDialog.ShowDialogAsync())
                {
                    Logger.LogInformation("[Home] [AppDisclaimer] Disclaimer declined, exiting...");
                    App.Current.Shutdown();
                    return false;
                }

                Logger.LogInformation("[Home] [AppDisclaimer] Disclaimer accepted.");
                Settings.IsDisclaimerAccepted = true;
                await Settings.SaveAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Home] [AppDisclaimer] An exception occurred showing the disclaimer.");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
                App.Current.Shutdown();
                return false;
            }
        }


        /// <summary>
        /// Shows the app setup dialog on the first run, or when any environment needs an update, or any backend has no installed environment, then refreshes the device table.
        /// </summary>
        private async Task AppSetupAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            var app = App.Current as App;
            try
            {
                // First Run, the setup changes the language without asking for a restart
                app?.SuppressLanguageNotice(true);

                var setupEnvironments = _environmentService.GetSetupEnvironments(!Settings.IsAppSetupAcknowledged);
                if (setupEnvironments.Count == 0 && Settings.IsFirstRunComplete)
                    return;

                Logger.LogInformation("[Home] [AppSetup] {Count} environments need to be installed or updated.", setupEnvironments.Count);
                var setupDialog = DialogService.GetDialog<AppSetupDialog>();
                await setupDialog.ShowDialogAsync(setupEnvironments);
                Logger.LogInformation("[Home] [AppSetup] App setup complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Home] [AppSetup] An exception occurred during app setup.");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                RefreshDevices();
                app?.SuppressLanguageNotice(false);
            }
        }


        /// <summary>
        /// Shows the app setup dialog with every backend, so the user can install or try other environments, then refreshes the device table.
        /// </summary>
        private async Task ManageEnvironmentsAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                Logger.LogInformation("[Home] [ManageEnvironments] Opening environment setup...");
                var setupDialog = DialogService.GetDialog<AppSetupDialog>();
                await setupDialog.ShowDialogAsync();
                Logger.LogInformation("[Home] [ManageEnvironments] Environment setup closed, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Home] [ManageEnvironments] An exception occurred managing environments.");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                RefreshDevices();
            }
        }


        /// <summary>
        /// Refreshes the new, recommended and recent models.
        /// </summary>
        private void RefreshModels()
        {
            var device = Settings.GetDefaultDevice();
            var models = Settings.DiffusionModels
                .Cast<IGenerateModel>()
                .Concat(Settings.LanguageModels)
                .Where(x => x.IsCompatible(device))
                .ToList();

            // New
            NewModels.Clear();
            foreach (var model in models.Where(x => x.IsNew).OrderBy(x => x.Name).Take(ModelCount))
                NewModels.Add(model);

            // Recommended
            RecommendedModels.Clear();
            foreach (var model in models.Where(x => x.IsRecommended).OrderBy(x => x.Name).Take(ModelCount))
                RecommendedModels.Add(model);

            // Recent
            RecentModels.Clear();
            var recentNames = HistoryService.HistoryCollection
                .Where(x => !string.IsNullOrEmpty(x.Model))
                .OrderByDescending(x => x.LastAccess)
                .Select(x => x.Model)
                .Distinct();
            foreach (var recentName in recentNames)
            {
                var model = models.FirstOrDefault(x => x.Name == recentName);
                if (model is null)
                    continue;

                RecentModels.Add(model);
                if (RecentModels.Count == ModelCount)
                    break;
            }
        }


        /// <summary>
        /// Navigates to the view.
        /// </summary>
        /// <param name="view">The view.</param>
        private async Task NavigateAsync(View view)
        {
            await NavigationService.NavigateAsync((int)view);
        }


        /// <summary>
        /// Opens an installed model in its view, otherwise selects it in the Models view.
        /// </summary>
        /// <param name="model">The model.</param>
        private async Task OpenModelAsync(IGenerateModel model)
        {
            if (model is null)
                return;

            try
            {
                // Installed
                if (model.Status == ModelStatusType.Installed)
                {
                    await OpenInstalledModelAsync(model);
                    return;
                }

                // Models
                Logger.LogInformation("[Home] [OpenModel] Opening model {Name} in Models", model.Name);
                await NavigationService.NavigateAsync((int)View.Models, new ModelOpenArgs(model));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Home] [OpenModel] An exception occurred opening model {Name}", model.Name);
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
        }


        /// <summary>
        /// Opens an installed model in its view.
        /// </summary>
        /// <param name="model">The model.</param>
        private async Task OpenInstalledModelAsync(IGenerateModel model)
        {
            var view = ViewManager.GetView(model);
            Logger.LogInformation("[Home] [OpenModel] Opening model {Name} in {View}", model.Name, view);
            await NavigationService.NavigateAsync((int)view, new ModelOpenArgs(model));
        }
    }
}
