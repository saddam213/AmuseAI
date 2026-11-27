using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Dialogs;
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
        private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Splashscreen _splashscreen = new();
        private static IHost _appHost;
        private static Mutex _appMutex;
        private static int _isShutdown;

        private static string _directoryBase;
        private static string _directoryData;
        private static string _directoryLogs;
        private static IHttpService _httpService;
        private readonly Settings _settings;
        private string _startupLanguage;
        private CultureInfo _startupCulture;
        private bool _isLanguageNoticeSuppressed;

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
            ApplyLanguage();
            _settings.PropertyChanged += async (s, e) => await OnSettingsChanged(e.PropertyName);
            builder.Services.AddWPFCommon<MainWindow, Settings>(_settings);

            // Add sService
            builder.Services.AddSingleton<IHardwareService, HardwareService>();
            builder.Services.AddSingleton<IMediaService, MediaService>();
            builder.Services.AddSingleton<IHistoryService, HistoryService>();
            builder.Services.AddSingleton<IUpscaleService, UpscaleService>();
            builder.Services.AddSingleton<IExtractService, ExtractService>();
            builder.Services.AddSingleton<IGenerateService, GenerateService>();
            builder.Services.AddSingleton<ILanguageService, LanguageService>();
            builder.Services.AddSingleton<IAudioService, AudioService>();
            builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
            builder.Services.AddSingleton<IInterpolationService, InterpolationService>();
            builder.Services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            builder.Services.AddSingleton<IModelAccessService, ModelAccessService>();
            builder.Services.AddSingleton<IMigrationService, MigrationService>();
            builder.Services.AddSingleton<IHttpService, HttpService>();
            builder.Services.AddSingleton<IPreviewService, PreviewService>();
            builder.Services.AddSingleton<IMcpService, McpService>();
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
            CopyStaticData();
            var configuration = SettingsManager.Load();
            configuration.Initialize(_directoryData).Wait();
            return configuration;
        }


        /// <summary>
        /// Copies the static data shipped in the install directory (default settings and templates) to the data directory.
        /// The installer runs as an administrator and only seeds that account's data directory, so every other user gets their copy here.
        /// Settings.default.json is moved to Settings.backup.json once merged, so either file being current means this version was already applied.
        /// </summary>
        private static void CopyStaticData()
        {
            if (!IsApplicationInstalled())
                return;

            CopyStaticFile("Settings.default.json", "Settings.backup.json");
            CopyStaticFile("Templates.json");
        }


        /// <summary>
        /// Copies a static file from the install directory to the data directory when it is missing or older than the installed file.
        /// </summary>
        /// <param name="relativePath">The file path, relative to both the install and data directories.</param>
        /// <param name="appliedFileName">The name of a data directory file that also marks the installed file as applied.</param>
        private static void CopyStaticFile(string relativePath, string appliedFileName = null)
        {
            var sourceFile = Path.Combine(_directoryBase, relativePath);
            var destinationFile = Path.Combine(_directoryData, relativePath);
            try
            {
                if (!File.Exists(sourceFile))
                    return;

                var sourceTime = File.GetLastWriteTimeUtc(sourceFile);
                if (IsFileCurrent(destinationFile, sourceTime))
                    return;

                if (appliedFileName != null && IsFileCurrent(Path.Combine(_directoryData, appliedFileName), sourceTime))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(sourceFile, destinationFile, true);
                Log.Logger.Information("[CopyStaticData] Copied static file, File: {File}", relativePath);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[CopyStaticData] Failed to copy static file, Source: {Source}, Destination: {Destination}", sourceFile, destinationFile);
            }
        }


        /// <summary>
        /// Determines whether a file exists and was written at or after the specified time.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="sourceTime">The last write time of the installed file, in UTC.</param>
        private static bool IsFileCurrent(string filePath, DateTime sourceTime)
        {
            return File.Exists(filePath) && File.GetLastWriteTimeUtc(filePath) >= sourceTime;
        }


        /// <summary>
        /// Applies the saved UI language before any view is created, an unset or unknown language keeps the system language.
        /// </summary>
        private void ApplyLanguage()
        {
            _startupCulture = CultureInfo.CurrentUICulture;
            if (string.IsNullOrEmpty(_settings.UILanguage))
                return;

            try
            {
                LocalizationService.Instance.SetLanguage(_settings.UILanguage);
                _startupLanguage = _settings.UILanguage;
                _splashscreen.UpdateTitle();
            }
            catch (CultureNotFoundException ex)
            {
                Log.Logger.Warning(ex, "[ApplyLanguage] Unknown UI language, Language: {Language}", _settings.UILanguage);
            }
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

            // Run Migrations, before the main window so the app setup sees the environments already deleted
            await migrationService.RunAutoMigrationsAsync();

            // Open Main Window
            MainWindow = _appHost.Services.GetMainWindow();
            MainWindow.Show();
            _splashscreen.Close();
            Log.Logger.Information($"[AppStartup] Application started.");
        }


        /// <summary>
        /// Application shutdown.
        /// </summary>
        private async Task AppShutdown()
        {
            // OnSessionEnding and OnExit can both run, only shut down once
            if (Interlocked.Exchange(ref _isShutdown, 1) == 1)
                return;

            Log.Logger.Information($"[AppShutdown] Shutting down application...");
            using (_appHost)
            {
                Log.Logger.Information($"[AppShutdown] Deleting Temp Directory.");
                if (!_settings.DeleteTempDirectory())
                {
                    Log.Logger.Information($"[AppShutdown] Failed to delete Temp Directory");
                }

                // Run each step even if an earlier one fails, so settings are always saved
                await RunShutdownStepAsync("Cancel", () => _cancellationTokenSource.CancelAsync());
                await RunShutdownStepAsync("SaveSettings", () => _settings.SaveAsync());
                await RunShutdownStepAsync("StopMcpServers", () => _appHost.Services.GetService<IMcpService>().DisconnectAsync());
                await RunShutdownStepAsync("StopHost", () => _appHost.StopAsync());
                DeregisterExceptionHandlers();
                _appMutex.WaitOne();
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
                FileQueue.Shutdown();

                // Log before _appHost is disposed, it owns and disposes the Serilog logger
                Log.Logger.Information($"[AppShutdown] Application shutdown complete.");
            }
        }


        /// <summary>
        /// Runs a shutdown step, logging and swallowing any exception.
        /// </summary>
        /// <param name="name">The step name.</param>
        /// <param name="step">The step.</param>
        private static async Task RunShutdownStepAsync(string name, Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[AppShutdown] {Step} failed.", name);
            }
        }


        /// <summary>
        /// Runs AppShutdown to completion before returning.
        /// WPF does not await async void overrides, so without this the process can exit mid-shutdown.
        /// Shutdown runs on the UI thread (parts of it are thread-affine), so pump the dispatcher
        /// while waiting rather than blocking it, otherwise anything that needs the UI thread deadlocks.
        /// </summary>
        private void AppShutdownBlocking()
        {
            var shutdownTask = AppShutdown();
            if (!shutdownTask.IsCompleted)
            {
                var frame = new DispatcherFrame();
                var timeoutTask = Task.WhenAny(shutdownTask, Task.Delay(_shutdownTimeout));
                timeoutTask.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
                try
                {
                    Dispatcher.PushFrame(frame);
                }
                catch (InvalidOperationException ex)
                {
                    // Dispatcher processing suspended, cannot wait for shutdown
                    Log.Logger.Error(ex, "[AppShutdown] Unable to wait for shutdown to complete.");
                }
            }

            if (!shutdownTask.IsCompleted)
            {
                Log.Logger.Warning("[AppShutdown] Shutdown did not complete within {Timeout}, exiting anyway.", _shutdownTimeout);
                return;
            }

            if (shutdownTask.IsFaulted)
            {
                Log.Logger.Error(shutdownTask.Exception, "[AppShutdown] An exception occurred during shutdown.");
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
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            AppShutdownBlocking();
            base.OnSessionEnding(e);
        }


        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Exit" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.Windows.ExitEventArgs" /> that contains the event data.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            AppShutdownBlocking();
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
        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Prevent application from crashing
            // Must be set before the handler returns, WPF reads it synchronously
            e.Handled = true;
            ShowExceptionMessage(e.Exception);
        }


        /// <summary>
        /// Handles the <see cref="E:AppDomainException" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnhandledExceptionEventArgs"/> instance containing the event data.</param>
        private void OnAppDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            // The process is terminating and this cannot be prevented, a dialog would never be shown
            // Log and flush so the cause is not lost
            Log.Logger.Fatal(e.ExceptionObject as Exception, "[Application] [Exception] An unhandled exception occurred, IsTerminating: {IsTerminating}", e.IsTerminating);
            if (e.IsTerminating)
                Log.CloseAndFlush();
        }


        /// <summary>
        /// Handles the <see cref="E:TaskSchedulerException" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnobservedTaskExceptionEventArgs"/> instance containing the event data.</param>
        private void OnTaskSchedulerException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Prevent application from crashing
            e.SetObserved();
            ShowExceptionMessage(e.Exception);
        }


        /// <summary>
        /// Shows the exception message.
        /// </summary>
        /// <param name="ex">The ex.</param>
        private static void ShowExceptionMessage(Exception ex)
        {
            Log.Logger.Error(ex, "[Application] [Exception] An unexpected exception occurred.");

            // Queue the dialog, never block the caller (may be the UI or finalizer thread)
            var dispatcher = Current?.Dispatcher;
            if (dispatcher is null || dispatcher.HasShutdownStarted)
                return;

            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, $"{AppErrors.UnexpectedError}:\n{ex.Message}");
                }
                catch (Exception dialogEx)
                {
                    Log.Logger.Error(dialogEx, "[Application] [Exception] Failed to show exception dialog.");
                }
            });
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
            else if (propertyName.Equals(nameof(Settings.Theme)))
            {
                await OnThemeChangedAsync();
            }
            else if (propertyName.Equals(nameof(Settings.UILanguage)))
            {
                await OnLanguageChangedAsync();
            }
        }


        /// <summary>
        /// Called when the UI language setting changes, saves settings and tells the user some text only updates after a restart.
        /// When no language was saved the app runs in the system language, choosing that language is not treated as a change.
        /// </summary>
        private async Task OnLanguageChangedAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.UILanguage))
                    return;

                await _settings.SaveAsync();
                Log.Logger.Information("[OnLanguageChanged] Language changed, Language: {Language}, Active: {Active}", _settings.UILanguage, _startupLanguage ?? _startupCulture?.Name);
                if (IsStartupLanguage(_settings.UILanguage))
                    return;

                // First Run, the setup changes the language without asking for a restart
                if (_isLanguageNoticeSuppressed)
                    return;

                await DialogService.ShowMessageAsync(AppDefault.LanguageChangedTitle, AppDefault.LanguageChangedMessage, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[OnLanguageChanged] An exception occurred handling the language change.");
            }
        }


        /// <summary>
        /// Called when the theme setting changes, saves settings and tells the user a restart is required.
        /// </summary>
        private async Task OnThemeChangedAsync()
        {
            try
            {
                // Save
                await _settings.SaveAsync();
                Log.Logger.Information("[OnThemeChanged] Theme changed, Theme: {Theme}, Active: {Active}", _settings.Theme, ThemeDictionary.Theme);

                // Switched back to the running theme, no restart needed
                if (_settings.Theme == ThemeDictionary.Theme)
                    return;

                // Notify
                await DialogService.ShowMessageAsync(AppDefault.ThemeChangedTitle, AppDefault.ThemeChangedMessage, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "[OnThemeChanged] An exception occurred handling the theme change.");
            }
        }


        /// <summary>
        /// Sets whether the language restart notice is suppressed, the first run setup changes the language without asking for a restart.
        /// </summary>
        /// <param name="isSuppressed">if set to <c>true</c> the notice is not shown.</param>
        public void SuppressLanguageNotice(bool isSuppressed)
        {
            _isLanguageNoticeSuppressed = isSuppressed;
        }


        /// <summary>
        /// Determines whether a language is the one the app started in: the saved language, or with none saved the system language.
        /// The system language is matched the way the language list picks it, its culture and parents first, then the same language.
        /// </summary>
        /// <param name="language">The language.</param>
        private bool IsStartupLanguage(string language)
        {
            if (!string.IsNullOrEmpty(_startupLanguage))
                return string.Equals(language, _startupLanguage, StringComparison.OrdinalIgnoreCase);

            if (_startupCulture == null)
                return false;

            for (var culture = _startupCulture; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
            {
                if (string.Equals(culture.Name, language, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            try
            {
                return string.Equals(CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName, _startupCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase);
            }
            catch (CultureNotFoundException)
            {
                return false;
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