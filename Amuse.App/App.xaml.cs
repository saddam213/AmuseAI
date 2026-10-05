using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static readonly string AppName = "Amuse";                            // Amuse
        public static readonly string AppPrefix = "Amuse_v";                        // Amuse_v
        public static readonly string AppVersion = GetAppVersion();                 // 0.3.0
        public static readonly string AppVersionTag = GetAppVersionTag();           // v0.3.0
        public static readonly string AppVersionDisplay = GetAppVersionDisplay();   // v0.3.0-dev
        public static readonly string AppDisplayName = GetAppDisplayName();         // Amuse v0.3.0-dev
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Splashscreen _splashscreen = new();
        private static IHost _appHost;
        private static Mutex _appMutex;
        private static string _directoryBase;
        private static string _directoryData;
        private static string _directoryLogs;
        private static IHttpService _httpService;
        private readonly Settings _settings;

        public App()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _appMutex = new Mutex(false, "Global\\TensorStack_Amuse", out bool isNewInstance);
            if (!isNewInstance)
            {
                ActivateExistingInstance();
                return;
            }

            RegisterExceptionHandlers();
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            // Paths
            _directoryBase = AppDomain.CurrentDomain.BaseDirectory;
            _directoryData = GetApplicationDataDirectory();
            _directoryLogs = Path.Combine(_directoryData, "Logs");

            // Host
            var builder = Host.CreateApplicationBuilder();

            // Logging
            ConfigureLogging();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger: Log.Logger, dispose: true);

            // Add TensorStack.WPF
            _settings = LoadSettingsFile();
            _settings.PropertyChanged += async (s, e) => await OnSettingsChanged(e.PropertyName);
            builder.Services.AddWPFCommon<MainWindow, Settings>(_settings);

            // Add sService
            builder.Services.AddSingleton<IHardwareService, HardwareService>();
            builder.Services.AddSingleton<IMediaService, MediaService>();
            builder.Services.AddSingleton<IHistoryService, HistoryService>();
            builder.Services.AddSingleton<IUpscaleService, UpscaleService>();
            builder.Services.AddSingleton<IExtractService, ExtractService>();
            builder.Services.AddSingleton<IGenerateService, GenerateService>();
            builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
            builder.Services.AddSingleton<IInterpolationService, InterpolationService>();
            builder.Services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            builder.Services.AddSingleton<IMigrationService, MigrationService>();
            builder.Services.AddSingleton<IHttpService, HttpService>();
            builder.Services.AddSingleton<IPreviewService, PreviewService>();
            builder.Services.AddSingleton<IToolService, ToolService>();

            // Build
            _appHost = builder.Build();

            // TensorStack.WPF
            _appHost.Services.UseWPFCommon();

            UpdateCommand = new AsyncRelayCommand(UpdateApplicationAsync);
            _ = AutoCheckForUpdates(_cancellationTokenSource.Token);
        }

        public static string DirectoryBase => _directoryBase;
        public static string DirectoryData => _directoryData;
        public static string DirectoryLogs => _directoryLogs;
        public static string DirectoryServer => _directoryBase;
        public AsyncRelayCommand UpdateCommand { get; set; }

        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>T.</returns>
        public static T GetService<T>() => _appHost.Services.GetService<T>();


        /// <summary>
        /// Gets an embedded resource.
        /// </summary>
        /// <param name="resourceName">Name of the resource.</param>
        public static string GetEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    return string.Empty;

                using (var streamReader = new StreamReader(resourceStream, Encoding.UTF8))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }


        /// <summary>
        /// Determines whether the application is installed.
        /// </summary>
        private static bool IsApplicationInstalled()
        {
#if RELEASE_INSTALLER
            return true;
#else
            return false;
#endif
        }


        /// <summary>
        /// Gets the application data directory.
        /// </summary>
        private static string GetApplicationDataDirectory()
        {
            if (IsApplicationInstalled())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Amuse");

            return _directoryBase;
        }


        /// <summary>
        /// Loads the settings file.
        /// </summary>
        private static Settings LoadSettingsFile()
        {
            var configuration = SettingsManager.Load();
            configuration.Initialize(_directoryData).Wait();
            return configuration;
        }


        /// <summary>
        /// Application startup.
        /// </summary>
        /// <returns>Task.</returns>
        private async Task AppStartup()
        {
            Log.Logger.Information($"[AppStartup] Starting application...");
            _httpService = _appHost.Services.GetService<IHttpService>();
            var historyService = _appHost.Services.GetService<IHistoryService>();
            var hardwareService = _appHost.Services.GetService<IHardwareService>();
            var migrationService = _appHost.Services.GetService<IMigrationService>();

            // Load History
            await historyService.InitializeAsync();

            // Load Devices
            Log.Logger.Information($"[AppStartup] Loading devices...");
            var devices = hardwareService.GetGPUDevices();
            _settings.InitializeDevices(devices);
            foreach (var device in devices)
            {
                Log.Logger.Information($"[AppStartup] Device found, Vendor: {device.Vendor}, Name: {device.Name}, DeviceId: {device.DeviceId}, PCIBusId: {device.PCIBusId}, DeviceType: {device.DeviceType}, HardwareLUID: {device.HardwareLUID}, Memory: {device.Memory}MB");
            }

            // Open Main Window
            MainWindow = _appHost.Services.GetMainWindow();
            MainWindow.Show();
            _splashscreen.Close();

            // Run Migrations
            await migrationService.RunAutoMigrationsAsync();
            Log.Logger.Information($"[AppStartup] Application started.");
        }


        /// <summary>
        /// Application shutdown.
        /// </summary>
        private async Task AppShutdown()
        {
            Log.Logger.Information($"[AppShutdown] Shutting down application...");
            using (_appHost)
            {
                Log.Logger.Information($"[AppShutdown] Deleting Temp Directory.");
                if (!_settings.DeleteTempDirectory())
                {
                    Log.Logger.Information($"[AppShutdown] Failed to delete Temp Directory");
                }
                await _cancellationTokenSource.CancelAsync();
                await _settings.SaveAsync();
                await _appHost.StopAsync();
                DeregisterExceptionHandlers();
                _appMutex.WaitOne();
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
                FileQueue.Shutdown();
            }
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Startup" /> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs" /> that contains the event data.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppStartup();
            base.OnStartup(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.SessionEnding" /> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.SessionEndingCancelEventArgs" /> that contains the event data.</param>
        protected override async void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            await AppShutdown();
            base.OnSessionEnding(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Exit" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.Windows.ExitEventArgs" /> that contains the event data.</param>
        protected async override void OnExit(ExitEventArgs e)
        {
            await AppShutdown();
            base.OnExit(e);
        }


        /// <summary>
        /// Registers the exception handlers.
        /// </summary>
        private void RegisterExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskSchedulerException;
        }


        /// <summary>
        /// Deregisters the exception handlers.
        /// </summary>
        private void DeregisterExceptionHandlers()
        {
            DispatcherUnhandledException -= OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainException;
            TaskScheduler.UnobservedTaskException -= OnTaskSchedulerException;
        }


        /// <summary>
        /// Handles the <see cref="E:DispatcherException" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DispatcherUnhandledExceptionEventArgs"/> instance containing the event data.</param>
        private async void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            await ShowExceptionMessage(e.Exception);

            // Prevent application from crashing
            e.Handled = true;
        }


        /// <summary>
        /// Handles the <see cref="E:AppDomainException" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnhandledExceptionEventArgs"/> instance containing the event data.</param>
        private async void OnAppDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                await ShowExceptionMessage(ex);
            }
        }


        /// <summary>
        /// Handles the <see cref="E:TaskSchedulerException" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnobservedTaskExceptionEventArgs"/> instance containing the event data.</param>
        private async void OnTaskSchedulerException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            await ShowExceptionMessage(e.Exception);

            // Prevent application from crashing
            e.SetObserved();
        }


        /// <summary>
        /// Shows the exception message.
        /// </summary>
        /// <param name="ex">The ex.</param>
        private static async Task ShowExceptionMessage(Exception ex)
        {
            Log.Logger.Error(ex, "[Application] [Exception] An unexpected exception occurred.");
            await App.Current.Dispatcher.InvokeAsync(() => DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, $"{AppErrors.UnexpectedError}:\n{ex.Message}"));
        }


        /// <summary>
        /// Gets the application version.
        /// </summary>
        private static string GetAppVersion()
        {
            var version = Assembly.GetEntryAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }


        /// <summary>
        /// Gets the application version tag.
        /// </summary>
        private static string GetAppVersionTag()
        {
            return $"v{AppVersion}";
        }


        /// <summary>
        /// Gets the application version display name.
        /// </summary>
        private static string GetAppVersionDisplay()
        {
            return $"{AppVersionTag}";
        }


        /// <summary>
        /// Gets the display name of the application.
        /// </summary>
        /// <returns>System.String.</returns>
        private static string GetAppDisplayName()
        {
            return $"{AppName} {AppVersionDisplay}";
        }


        /// <summary>
        /// Configures the logging.
        /// </summary>
        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(GetLogName(), rollOnFileSizeLimit: true)
                .CreateLogger();
        }


        /// <summary>
        /// Gets the name of the log.
        /// </summary>
        private static string GetLogName()
        {
            var now = DateTime.Now;
            return Path.Combine(_directoryLogs, @$"Amuse-{DateTime.Now:dd-MM-yyyy}-{now.Hour * 3600 + now.Minute * 60 + now.Second}.txt");
        }


        /// <summary>
        /// Called when Settings property changed
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private async Task OnSettingsChanged(string propertyName)
        {
            if (propertyName.Equals(nameof(Settings.IsAutoUpdateEnabled)))
            {
                await CheckForUpdates();
            }
        }


        /// <summary>
        /// Gets the update information from Github.
        /// </summary>
        /// <returns>AppUpdate.</returns>
        private async Task<AppUpdate> GetUpdateInfo()
        {
            try
            {
                Log.Logger.Information("[GetUpdateInfo] - Check for update...");
                using (var response = await _httpService.Client.GetAsync("https://api.github.com/repos/saddam213/AmuseAI/releases/latest"))
                {
                    response.EnsureSuccessStatusCode();
                    var versionResponse = await response.Content.ReadFromJsonAsync<AppVersion>();
                    if (versionResponse == null)
                    {
                        Log.Logger.Error("[GetUpdateInfo] - Null response from update check.");
                        return default;
                    }

                    Log.Logger.Information("[GetUpdateInfo] - Check for update success.");
                    return new AppUpdate(versionResponse, AppPrefix);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("[GetUpdateInfo] - An exception occured during update check, Error: {Message}", ex.Message);
                return default;
            }
        }


        /// <summary>
        /// Checks for updates.
        /// </summary>
        private async Task CheckForUpdates()
        {
            try
            {
                if (_settings.IsAutoUpdateEnabled)
                {
                    Log.Logger.Information("[CheckForUpdates] - Check for updates...");
                    var updateResponse = await GetUpdateInfo();
                    if (updateResponse is not null)
                    {
                        _settings.IsUpdateAvailable = updateResponse.Version != AppVersionTag;
                        Log.Logger.Information($"[CheckForUpdates] - Check for updates complete, IsUpdateAvailable: {_settings.IsUpdateAvailable}");
                    }
                    else
                    {
                        Log.Logger.Error("[CheckForUpdates] - Null response from update check.");
                    }
                }
                else
                {
                    _settings.IsUpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[CheckForUpdates] - An exception occured during update check, Error: {ex.Message}");
            }
        }


        /// <summary>
        /// Automaticly the check for updates.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task AutoCheckForUpdates(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                while (true)
                {
                    await CheckForUpdates();
                    await Task.Delay(TimeSpan.FromMinutes(60), cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
        }


        /// <summary>
        /// Update the application
        /// </summary>
        /// <exception cref="FileNotFoundException">Update File Not Found</exception>
        private async Task UpdateApplicationAsync()
        {
            try
            {
                var updateInfo = await GetUpdateInfo();
                if (updateInfo is null)
                    return;

                Log.Logger.Information($"[UpdateAsync] - Show UpdateDialog dialog");
                var isInstalled = IsApplicationInstalled();
                var downloadDialog = DialogService.GetDialog<AppUpdateDialog>();
                if (await downloadDialog.ShowDialogAsync(updateInfo, isInstalled))
                {
                    if (!File.Exists(downloadDialog.AssetLocation))
                        throw new FileNotFoundException("Update File Not Found", downloadDialog.AssetLocation);

                    Log.Logger.Information("[UpdateAsync] - Update downloaded successfully, Launching: {FileName}", downloadDialog.AssetLocation);
                    if (isInstalled)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            Verb = "runas",
                            UseShellExecute = true,
                            FileName = downloadDialog.AssetLocation,
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = _settings.DirectoryTemp,
                        });
                    }

                    Log.Logger.Information($"[UpdateAsync] - Launched Amuse installer, closing application...");
                    Current.Shutdown();
                    return;
                }

                Log.Logger.Information($"[UpdateAsync] - User canceled update.");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[UpdateAsync] - An exception occured during update navigate");
            }
        }


        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private void ActivateExistingInstance()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
            Environment.Exit(0);
        }
    }
}