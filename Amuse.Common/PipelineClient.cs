using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public class PipelineClient : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ClientConfig _config;
        private readonly ServerConfig _serverConfig;
        private readonly NamedPipeClientStream _commandChannel;
        private readonly NamedPipeClientStream _pipelineChannel;
        private readonly NamedPipeClientStream _progressChannel;
        private readonly PipelineTensorChannel _pipelineTensorChannel;
        private readonly ProcessHandler _processHandler;
        private readonly IProgress<PipelineProgress> _progressCallback;

        private CancellationTokenSource _progressCancellation;
        private Process _serverProcess;
        private bool _isCanceled;

        protected PipelineClient(ClientConfig config, ServerConfig serverConfig, IProgress<PipelineProgress> progressCallback, ILogger logger = default)
        {
            _logger = logger;
            _config = config;
            _serverConfig = serverConfig;
            _progressCallback = progressCallback;
            _processHandler = new ProcessHandler();
            _progressCancellation = new CancellationTokenSource();
            _commandChannel = new NamedPipeClientStream(".", _serverConfig.ChannelCommand, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipelineChannel = new NamedPipeClientStream(".", _serverConfig.ChannelPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _progressChannel = new NamedPipeClientStream(".", _serverConfig.ChannelProgress, PipeDirection.In, PipeOptions.Asynchronous);
            _pipelineTensorChannel = new PipelineTensorChannel(_serverConfig);
            _ = ProcessProgressQueueAsync(_progressCallback);
        }

        public PipelineClient(ClientConfig config, IProgress<PipelineProgress> progressCallback, ILogger logger = default)
            : this(config, ServerConfig.GetConfig(config.ServerType), progressCallback, logger) { }


        /// <summary>
        /// Start client as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task StartAsync(CancellationToken cancellationToken)
        {
            _isCanceled = false;

            // Start Server
            await StartServerAsync();

            try
            {
                // Connect Pipes
                await Task.WhenAll
                (
                    _commandChannel.ConnectAsync(cancellationToken),
                    _progressChannel.ConnectAsync(cancellationToken),
                    _pipelineChannel.ConnectAsync(cancellationToken)
                );

                // Start Environment
                await SendPipelineRequestAsync(new PipelineRequest(RequestType.Start), cancellationToken);
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
            _isCanceled = true;
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
            var response = await SendPipelineRequest(request, cancellationToken);
            if (response.IsError)
            {
                if (response.IsCanceled)
                    throw new OperationCanceledException(response.Error);

                throw new Exception(response.Error);
            }
            return response;
        }


        /// <summary>
        /// Send a Pipeline request to the Server with memory mapped tensor inputs
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual async Task<PipelineResponse> SendPipelineTensorRequestAsync(PipelineRequest request, CancellationToken cancellationToken = default)
        {
            using (var tensorChannel = _pipelineTensorChannel.WriteRequest(request))
            {
                var response = await SendPipelineRequest(request, cancellationToken);
                _pipelineTensorChannel.ReadResponse(response);
                await SendTensorResponseComplete(cancellationToken);
                if (response.IsError)
                {
                    if (response.IsCanceled)
                        throw new OperationCanceledException(response.Error);

                    throw new Exception(response.Error);
                }
                return response;
            }
        }


        /// <summary>
        /// Send a CommandRequest to the Server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual async Task<CommandResponse> SendCommandRequestAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            await _commandChannel.SendMessage(request, cancellationToken);
            var response = await _commandChannel.ReceiveMessage<CommandResponse>(cancellationToken);
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
        protected virtual Task StartServerAsync()
        {
            var processInfo = new ProcessStartInfo
            {
                CreateNoWindow = !_config.IsDebugMode,
                UseShellExecute = false,
                FileName = Path.Combine(_config.ServerPath, _serverConfig.Executable),
                Arguments = _serverConfig.Arguments.IsNullOrEmpty() ? null : string.Join(' ', _serverConfig.Arguments)
            };

            // Environment Variables
            if (!_config.ServerVariables.IsNullOrEmpty())
            {
                foreach (var variable in _config.ServerVariables)
                    processInfo.Environment.Add(variable);
            }

            _serverProcess = Process.Start(processInfo);
            _processHandler.AddProcess(_serverProcess);
            return Task.CompletedTask;
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
            while (!_progressCancellation.IsCancellationRequested)
            {
                try
                {
                    if (!_progressChannel.IsConnected)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    var progress = await _progressChannel.ReceiveTensorMessage<PipelineProgress>(_progressCancellation.Token);
                    if (progress == null || _isCanceled)
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
            _progressCancellation?.SafeCancel();
            _progressCancellation?.Dispose();
            _progressChannel?.Dispose();
            _commandChannel?.Dispose();
            _pipelineChannel?.Dispose();
            _serverProcess?.Dispose();
        }


        /// <summary>
        /// Create the pipeline
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task CreateAsync(PipelineCreateOptions options, CancellationToken cancellationToken = default)
        {
            try
            {
                _isCanceled = false;
                await StartAsync(cancellationToken);
                await SendPipelineRequestAsync(new PipelineRequest(options), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
            catch (Exception)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Load the pipeline
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task LoadAsync(PipelineLoadOptions pipeline, CancellationToken cancellationToken = default)
        {
            try
            {
                _isCanceled = false;
                await SendPipelineRequestAsync(new PipelineRequest(pipeline, RequestType.Load), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ReloadAsync(PipelineReloadOptions options, CancellationToken cancellationToken = default)
        {
            try
            {
                _isCanceled = false;
                await SendPipelineRequestAsync(new PipelineRequest(options), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            _isCanceled = true;
            await SendPipelineRequestAsync(new PipelineRequest(RequestType.Unload));
            await StopAsync();
        }


        /// <summary>
        /// Cancel pipeline Load/Run
        /// </summary>
        public async Task CancelAsync()
        {
            _isCanceled = true;
            await SendCommandRequestAsync(new CommandRequest());
        }


        /// <summary>
        /// Generates Image.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<ImageTensor>> GenerateImageAsync(GenerateImageOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                ImageOptions = options
            }));
            return response.ImageTensors;
        }


        /// <summary>
        /// Generates Video.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<VideoSequence>> GenerateVideoAsync(GenerateVideoOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                VideoOptions = options
            }));
            return response.VideoSequences;
        }


        /// <summary>
        /// Generates Audio.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<AudioTensor>> GenerateAudioAsync(GenerateAudioOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                AudioOptions = options
            }));
            return response.AudioTensors;
        }


        /// <summary>
        /// Generates Text.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<TextInput>> GenerateTextAsync(GenerateTextOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                TextOptions = options
            }));
            return response.TextResponse;
        }


        /// <summary>
        /// Upscales Image.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<ImageTensor>> UpscaleImageAsync(UpscaleImageRunOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                UpscaleImageOptions = options
            }));
            return response.ImageTensors;
        }


        /// <summary>
        /// Upscales Video.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<VideoSequence>> UpscaleVideoAsync(UpscaleVideoRunOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                UpscaleVideoOptions = options
            }));
            return response.VideoSequences;
        }


        /// <summary>
        /// Extracts Image.
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<IReadOnlyList<ImageTensor>> ExtractImageAsync(ExtractImageRunOptions options)
        {
            _isCanceled = false;
            var response = await SendPipelineTensorRequestAsync(new PipelineRequest(new PipelineRunOptions
            {
                ExtractImageOptions = options
            }));
            return response.ImageTensors;
        }


        /// <summary>
        /// Sends the pipeline request, waits and returns the response
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>PipelineResponse.</returns>
        private async Task<PipelineResponse> SendPipelineRequest(PipelineRequest request, CancellationToken cancellationToken = default)
        {
            await _pipelineChannel.SendMessage(request, cancellationToken);
            return await _pipelineChannel.ReceiveMessage<PipelineResponse>(cancellationToken);
        }


        /// <summary>
        /// Sends the tensor response complete.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task SendTensorResponseComplete(CancellationToken cancellationToken = default)
        {
            var message = new CommandRequest(CommandRequestType.Complete);
            await _commandChannel.SendMessage(message, cancellationToken);
            await _commandChannel.ReceiveMessage<CommandResponse>(cancellationToken);
        }
    }
}
