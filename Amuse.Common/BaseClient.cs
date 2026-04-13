using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Python.Common;

namespace Amuse.Common
{
    public abstract class BaseClient : IDisposable
    {
        private readonly ProcessLifetimeHandler _processHandler;
        private Process _serverProcess;

        public BaseClient(ClientConfig config, ChannelConfig channelConfig, IProgress<PipelineProgress> progressCallback, ILogger logger = default)
        {
            Logger = logger;
            Config = config;
            ChannelConfig = channelConfig;
            ProgressCallback = progressCallback;
            _processHandler = new ProcessLifetimeHandler();
            ProgressCancellation = new CancellationTokenSource();
            CommandChannel = new NamedPipeClientStream(".", ChannelConfig.ChannelCommand, PipeDirection.InOut, PipeOptions.Asynchronous);
            PipelineChannel = new NamedPipeClientStream(".", ChannelConfig.ChannelPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            ProgressChannel = new NamedPipeClientStream(".", ChannelConfig.ChannelProgress, PipeDirection.In, PipeOptions.Asynchronous);
            _ = ProcessProgressQueueAsync(ProgressCallback);
        }

        protected ILogger Logger { get; }
        protected ClientConfig Config { get; }
        protected ChannelConfig ChannelConfig { get; }
        protected NamedPipeClientStream CommandChannel { get; }
        protected NamedPipeClientStream PipelineChannel { get; }
        protected NamedPipeClientStream ProgressChannel { get; }
        protected IProgress<PipelineProgress> ProgressCallback { get; }
        protected CancellationTokenSource ProgressCancellation { get; }
        protected bool IsCanceled { get; set; }


        /// <summary>
        /// Start client as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task StartAsync(EnvironmentMode mode, CancellationToken cancellationToken)
        {
            IsCanceled = false;

            // Start Server
            await StartServerAsync();

            try
            {
                // Connect Pipes
                await Task.WhenAll
                (
                    CommandChannel.ConnectAsync(cancellationToken),
                    ProgressChannel.ConnectAsync(cancellationToken),
                    PipelineChannel.ConnectAsync(cancellationToken)
                );

                // Start Environment
                await SendPipelineRequestAsync(new PipelineRequest(RequestType.Start), cancellationToken);
                await SendPipelineRequestAsync(new PipelineRequest(Config.Environment, mode), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Stop client
        /// </summary>
        public async Task StopAsync()
        {
            IsCanceled = true;
            await SendPipelineRequestAsync(new PipelineRequest(RequestType.Stop));
            await StopServerAsync(_serverProcess);
        }


        /// <summary>
        /// Kill server.
        /// </summary>
        public virtual async Task KillServerAsync()
        {
            if (_serverProcess is not null)
            {
                _serverProcess.Kill(true);
                await _serverProcess.WaitForExitAsync();
            }
        }


        /// <summary>
        /// Send a Pipeline request to the Server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual async Task<PipelineResponse> SendPipelineRequestAsync(PipelineRequest request, CancellationToken cancellationToken = default)
        {
            await PipelineChannel.SendMessage(request, cancellationToken);
            var response = await PipelineChannel.ReceiveMessage<PipelineResponse>(cancellationToken);
            if (response.IsError)
            {
                if (response.IsCanceled)
                    throw new OperationCanceledException(response.Error);

                throw new Exception(response.Error);
            }
            return response;
        }


        /// <summary>
        /// Send a Object request to the Server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual async Task<CommandResponse> SendObjectRequestAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            await CommandChannel.SendObject(request, cancellationToken);
            var response = await CommandChannel.ReceiveObject<CommandResponse>(cancellationToken);
            if (response.IsError)
            {
                if (response.IsCanceled)
                    throw new OperationCanceledException(response.Error);

                throw new Exception(response.Error);
            }
            return response;
        }


        /// <summary>
        /// Start server
        /// </summary>
        protected virtual async Task StartServerAsync()
        {
            var existingProcess = Process.GetProcesses().FirstOrDefault(x => x.ProcessName == ChannelConfig.Name);
            if (existingProcess is not null)
                await StopServerAsync(existingProcess);

            var processInfo = new ProcessStartInfo
            {
                CreateNoWindow = !Config.IsDebugMode,
                UseShellExecute = false,
                FileName = Path.Combine(Config.ServerPath, ChannelConfig.Executable),
                Arguments = ChannelConfig.Arguments.IsNullOrEmpty() ? null : string.Join(' ', ChannelConfig.Arguments)
            };

            // Environment Variables
            if (!Config.Environment.Variables.IsNullOrEmpty())
            {
                foreach (var variable in Config.Environment.Variables)
                    processInfo.Environment.Add(variable);
            }
            _serverProcess = Process.Start(processInfo);
            _processHandler.AddProcess(_serverProcess);
        }


        /// <summary>
        /// Stop server
        /// </summary>
        /// <param name="serverProcess">The server process.</param>
        /// <param name="timeout">The timeout.</param>
        protected virtual async Task StopServerAsync(Process serverProcess, int timeout = 5000)
        {
            using (serverProcess)
            {
                var timeoutDelay = Task.Delay(timeout);
                await Task.WhenAny(timeoutDelay, serverProcess.WaitForExitAsync());
                if (!serverProcess.HasExited)
                {
                    serverProcess.Kill(true);
                    await serverProcess.WaitForExitAsync();
                }
            }
        }


        /// <summary>
        /// Process the progress queue
        /// </summary>
        /// <param name="progressCallback">The progress callback.</param>
        protected virtual async Task ProcessProgressQueueAsync(IProgress<PipelineProgress> progressCallback)
        {
            while (!ProgressCancellation.IsCancellationRequested)
            {
                try
                {
                    if (!ProgressChannel.IsConnected)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    var progress = await ProgressChannel.ReceiveObject<PipelineProgress>(ProgressCancellation.Token);
                    if (progress == null || IsCanceled)
                        continue;

                    progressCallback?.Report(progress);
                }
                catch (OperationCanceledException) { }
                catch (Exception)
                {
                    // _logger?.LogError(ex, $"[PipelineClient] [ProcessProgressQueueAsync] - An exception occurred processing progress");
                }
            }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            ProgressCancellation?.SafeCancel();
            ProgressCancellation?.Dispose();
            ProgressChannel?.Dispose();
            CommandChannel?.Dispose();
            PipelineChannel?.Dispose();
            _serverProcess?.Dispose();
        }
    }
}
