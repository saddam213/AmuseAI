using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using Logger = Microsoft.Extensions.Logging.ILogger;

namespace Amuse.Host
{
    internal class Program
    {
        private static Logger _logger;
        private static string _directoryBase;
        private static string _directoryData;
        private static ChannelConfig _channelConfig;

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        static async Task Main(string[] args)
        {
            _channelConfig = args.IsNullOrEmpty() || !args[0].Equals("download", StringComparison.OrdinalIgnoreCase)
                ? ChannelConfig.PipelineConfig
                : ChannelConfig.DownloadConfig;

            using (var mutex = new Mutex(true, $"Global\\{_channelConfig.Name}", out var createdNew))
            {
                if (!createdNew)
                    throw new InvalidOperationException("Another instance of this application is already running.");

                _directoryBase = AppDomain.CurrentDomain.BaseDirectory;
                _directoryData = GetApplicationDataDirectory();
                _logger = ConfigureLogging();
                await StartAsync();
            }
        }


        /// <summary>
        /// Start the server
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private static async Task StartAsync()
        {
            try
            {
                _logger.LogInformation("[StartAsync] Starting {Name}...", _channelConfig.Name);
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => cancellationTokenSource.SafeCancel();

                    // Start Server
                    using (var pipelineServer = new PipelineServer(_directoryData, _channelConfig, _logger))
                    {
                        await pipelineServer.StartAsync(cancellationTokenSource.Token);
                    }
                }
                _logger.LogInformation("[StartAsync] {Name} stopped.", _channelConfig.Name);
            }
            catch (EndOfStreamException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StartAsync] An unhandled exception occurred.");
            }
        }


        /// <summary>
        /// Configures the logging.
        /// </summary>
        private static Logger ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(GetLogName(), rollOnFileSizeLimit: true)
                .CreateLogger();
            var factory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });
            return factory.CreateLogger<Program>();
        }


        /// <summary>
        /// Gets the application data directory.
        /// </summary>
        private static string GetApplicationDataDirectory()
        {
#if RELEASE_INSTALLER
             return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Amuse");
#else
            return _directoryBase;
#endif
        }


        /// <summary>
        /// Gets the name of the log.
        /// </summary>
        private static string GetLogName()
        {
            var now = DateTime.Now;
            return Path.Combine(_directoryData, @$"Logs\{_channelConfig.Name}-{DateTime.Now:dd-MM-yyyy}-{now.Hour * 3600 + now.Minute * 60 + now.Second}.txt");
        }
    }
}
