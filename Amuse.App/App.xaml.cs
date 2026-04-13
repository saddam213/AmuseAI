using Amuse.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        public static readonly string AppVersion = GetAppVersion();                 // 0.3.0
        public static readonly string AppVersionTag = GetAppVersionTag();           // v0.3.0
        public static readonly string AppVersionDisplay = GetAppVersionDisplay();   // v0.3.0-dev
        public static readonly string AppDisplayName = GetAppDisplayName();         // Amuse v0.3.0-dev
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Splashscreen _splashscreen = new();
        private static IHost _appHost;
        private static Mutex _appMutex;
        private static string _directoryBase;
        private static string _directoryData;
        private static string _directoryPython;
        private readonly Settings _settings;

        public App()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AmuseApp");
            _appMutex = new Mutex(false, "Global\\TensorStack_Amuse", out bool isNewInstance);
            if (!isNewInstance)
            {
                ActivateExistingInstance();
                return;
            }

            RegisterExceptionHandlers();

            // Paths
            _directoryBase = AppDomain.CurrentDomain.BaseDirectory;
            _directoryData = GetApplicationDataDirectory();
            _directoryPython = Path.Combine(_directoryData, "PythonRuntime");

            // Host
            var builder = Host.CreateApplicationBuilder();

            // Logging
            ConfigureLogging();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger: Log.Logger, dispose: true);

            // Add TensorStack.WPF
            _settings = LoadSettingsFile();
            builder.Services.AddWPFCommon<MainWindow, Settings>(_settings);

            // Add sService
            builder.Services.AddSingleton<IHardwareService, HardwareService>();
            builder.Services.AddSingleton<IMediaService, MediaService>();
            builder.Services.AddSingleton<IHistoryService, HistoryService>();
            builder.Services.AddSingleton<IUpscaleService, UpscaleService>();
            builder.Services.AddSingleton<IExtractService, ExtractService>();
            builder.Services.AddSingleton<IDiffusionService, DiffusionService>();
            builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
            builder.Services.AddSingleton<IInterpolationService, InterpolationService>();
            builder.Services.AddSingleton<IAudioService, AudioService>();
            builder.Services.AddSingleton<IDownloadService, Services.DownloadService>();

            // Build
            _appHost = builder.Build();

            // TensorStack.WPF
            _appHost.Services.UseWPFCommon();

            UpdateCommand = new AsyncRelayCommand(UpdateApplicationAsync);
            _ = AutoCheckForUpdates(_cancellationTokenSource.Token);
        }

        public static string DirectoryBase => _directoryBase;
        public static string DirectoryData => _directoryData;
        public static string DirectoryPython => _directoryPython;
        public static string DirectoryServer => _directoryBase;
        public AsyncRelayCommand UpdateCommand { get; set; }

        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>T.</returns>
        public static T GetService<T>() => _appHost.Services.GetService<T>();


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
            var historyService = _appHost.Services.GetService<IHistoryService>();
            var hardwareService = _appHost.Services.GetService<IHardwareService>();

            // Load History
            await historyService.InitializeAsync();

            // Load Devices
            var devices = hardwareService.GetGPUDevices();
            _settings.InitializeDevices(devices);
            _settings.PropertyChanged += async (s, e) => await OnSettingsChanged(e.PropertyName);

            // Open Main Window
            MainWindow = _appHost.Services.GetMainWindow();
            MainWindow.Show();
            _splashscreen.Close();
        }


        /// <summary>
        /// Application shutdown.
        /// </summary>
        private async Task AppShutdown()
        {
            using (_appHost)
            {
                await _cancellationTokenSource.CancelAsync();
                await SettingsManager.SaveAsync(_settings);
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
            await DialogService.ShowErrorAsync("Unexpected Error", $"An unexpected error occurred:\n{ex.Message}");
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
            return $"{AppVersionTag}-dev";
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
            return Path.Combine(_directoryData, @$"Logs\Amuse-{DateTime.Now:dd-MM-yyyy}-{now.Hour * 3600 + now.Minute * 60 + now.Second}.txt");
        }


        /// <summary>
        /// Called when Settings property changed
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private async Task OnSettingsChanged(string propertyName)
        {
            if (propertyName.Equals(nameof(Settings.IsUpdateEnabled)))
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
                using (var response = await _httpClient.GetAsync("https://api.github.com/repos/TensorStack-AI/Amuse/releases/latest"))
                {
                    response.EnsureSuccessStatusCode();
                    var versionResponse = await response.Content.ReadFromJsonAsync<AppVersion>();
                    if (versionResponse == null)
                    {
                        Log.Logger.Error("[GetUpdateInfo] - Null response from update check.");
                        return default;
                    }

                    Log.Logger.Information("[GetUpdateInfo] - Check for update success.");
                    return new AppUpdate(versionResponse);
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
                if (_settings.IsUpdateEnabled)
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
                var downloadLink = isInstalled ? updateInfo.LinkInstaller : updateInfo.LinkStandalone;
                var updateFileName = Path.Combine(_settings.DirectoryTemp, Path.GetFileName(downloadLink));
                if (await DialogService.DownloadAsync($"Download Amuse {updateInfo.Version}?", downloadLink, updateFileName))
                {
                    if (!File.Exists(updateFileName))
                        throw new FileNotFoundException("Update File Not Found", updateFileName);

                    Log.Logger.Information("[UpdateAsync] - Update downloaded successfully, Launching: {FileName}", updateFileName);
                    if (isInstalled)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            Verb = "runas",
                            UseShellExecute = true,
                            FileName = updateFileName,
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