using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
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
        private readonly NamedPipeClientStream _streamChannel;
        private readonly PipelineTensorChannel _pipelineTensorChannel;
        private readonly PipelineStreamChannel _pipelineStreamChannel;
        private readonly ProcessHandler _processHandler;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private readonly SemaphoreSlim _pipelineLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);
        private readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(15);

        private CancellationTokenSource _progressCancellation;
        private Process _serverProcess;
        private bool _isCanceled;
        private int _streamId;

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
            _streamChannel = new NamedPipeClientStream(".", _serverConfig.ChannelStream, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipelineTensorChannel = new PipelineTensorChannel(_serverConfig);
            _pipelineStreamChannel = new PipelineStreamChannel(_streamChannel, _serverConfig, false);
            _ = Task.Run(() => ProcessProgressQueueAsync(_progressCallback));
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
                await ConnectAsync(cancellationToken);
                _ = Task.Run(() => _pipelineStreamChannel.StartAsync(_progressCancellation.Token));

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
        /// Connect the pipes, fails if the host exits or does not create its pipes in time.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="System.Exception">Host process exited before connecting.</exception>
        /// <exception cref="System.TimeoutException">Timed out connecting to the host process.</exception>
        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            using (var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectCancellation.CancelAfter(_connectTimeout);
                var connectTask = Task.WhenAll
                (
                    _commandChannel.ConnectAsync(connectCancellation.Token),
                    _progressChannel.ConnectAsync(connectCancellation.Token),
                    _streamChannel.ConnectAsync(connectCancellation.Token),
                    _pipelineChannel.ConnectAsync(connectCancellation.Token)
                );

                try
                {
                    var exitTask = _serverProcess.WaitForExitAsync(connectCancellation.Token);
                    if (await Task.WhenAny(connectTask, exitTask) == exitTask && _serverProcess.HasExited)
                    {
                        var exitCode = _serverProcess.ExitCode;
                        throw new Exception($"Host process exited before connecting, ExitCode: {exitCode}");
                    }
                    await connectTask;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Timed out connecting to the host process.");
                }
                finally
                {
                    connectCancellation.Cancel();
                }
            }
        }


        /// <summary>
        /// Stop client
        /// </summary>
        public async Task StopAsync()
        {
            _isCanceled = true;
            try
            {
                using (var stopCancellation = new CancellationTokenSource(_stopTimeout))
                {
                    await SendPipelineRequestAsync(new PipelineRequest(RequestType.Stop), stopCancellation.Token);
                }
                await StopServerAsync(_serverProcess);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[PipelineClient] [Stop] Host did not stop, killing process, Reason: {Reason}", ex.Message);
                await KillServerAsync();
            }
        }


        /// <summary>
        /// Kill server.
        /// </summary>
        public virtual async Task KillServerAsync()
        {
            if (_serverProcess is null)
                return;

            try
            {
                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill(true);
                    await _serverProcess.WaitForExitAsync();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited or disposed
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
            var response = await SendCommandRequest(request, cancellationToken);
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
                    processInfo.Environment[variable.Key] = variable.Value;
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
            _pipelineStreamChannel?.Dispose();
            _streamChannel?.Dispose();
            try
            {
                if (_serverProcess is not null && !_serverProcess.HasExited)
                    _serverProcess.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited or disposed
            }
            _serverProcess?.Dispose();
            _processHandler?.Dispose();
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
            try
            {
                using (var stopCancellation = new CancellationTokenSource(_stopTimeout))
                {
                    await SendPipelineRequestAsync(new PipelineRequest(RequestType.Unload), stopCancellation.Token);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[PipelineClient] [Unload] Host did not unload, killing process, Reason: {Reason}", ex.Message);
                await KillServerAsync();
                return;
            }
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
        /// Runs a pipeline stream, the input items are sent to the host as it takes them and its results are returned as they are produced.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="input">The items to stream to the host, null if the pipeline takes no stream input.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async IAsyncEnumerable<PipelineStreamItem> RunStreamAsync(PipelineRunOptions options, IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _isCanceled = false;
            await _pipelineLock.WaitAsync(cancellationToken);

            var isCompleted = false;
            var streamResult = new StreamResult();
            var inputCancellation = new CancellationTokenSource();
            try
            {
                var streamId = ++_streamId;
                _pipelineStreamChannel.Open(streamId);
                await _pipelineChannel.SendMessage(new PipelineRequest(options, streamId), CancellationToken.None);
                streamResult.ResponseTask = _pipelineChannel.ReceiveMessage<PipelineResponse>(CancellationToken.None);

                var readyTask = _pipelineStreamChannel.WaitReadyAsync();
                if (await Task.WhenAny(readyTask, streamResult.ResponseTask) == readyTask)
                {
                    await readyTask;
                    streamResult.IsStarted = true;
                    streamResult.InputTask = SendStreamInputAsync(input, inputCancellation.Token);
                    await foreach (var item in _pipelineStreamChannel.ReadAllAsync(cancellationToken))
                    {
                        yield return item;
                    }
                }
                isCompleted = true;
            }
            finally
            {
                await CompleteStreamAsync(streamResult, inputCancellation, isCompleted);
                _pipelineLock.Release();
            }

            if (streamResult.InputException is not null)
                throw streamResult.InputException;
            if (streamResult.Response is null)
                throw new Exception("The host closed the stream without a response.");
            if (streamResult.Response.IsError)
            {
                if (streamResult.Response.IsCanceled)
                    throw new OperationCanceledException(streamResult.Response.Error);

                throw new Exception(streamResult.Response.Error);
            }
        }


        /// <summary>
        /// Sends the input items to the host, completing the stream with the exception if the input fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task SendStreamInputAsync(IAsyncEnumerable<PipelineStreamItem> input, CancellationToken cancellationToken)
        {
            try
            {
                if (input is not null)
                {
                    await foreach (var item in input.WithCancellation(cancellationToken))
                    {
                        await _pipelineStreamChannel.WriteAsync(item, cancellationToken);
                    }
                }
                await _pipelineStreamChannel.CompleteAsync();
            }
            catch (OperationCanceledException)
            {
                // The host stopped reading or the stream was cancelled
            }
            catch (Exception ex)
            {
                await _pipelineStreamChannel.CompleteAsync(ex);
                throw;
            }
        }


        /// <summary>
        /// Completes a stream run, an unfinished stream is cancelled on the host and the final response is always read so the pipeline channel stays in sync.
        /// </summary>
        /// <param name="streamResult">The stream result.</param>
        /// <param name="inputCancellation">The input cancellation.</param>
        /// <param name="isCompleted">if set to <c>true</c> the host completed the stream.</param>
        private async Task CompleteStreamAsync(StreamResult streamResult, CancellationTokenSource inputCancellation, bool isCompleted)
        {
            try
            {
                if (!isCompleted && streamResult.IsStarted)
                {
                    _isCanceled = true;
                    await SendCommandRequest(new CommandRequest());
                }
            }
            catch (Exception)
            {
                // The host is gone, the response read below fails
            }

            try
            {
                if (streamResult.ResponseTask is not null)
                    streamResult.Response = await streamResult.ResponseTask;
            }
            catch (Exception)
            {
                // The host is gone, there is no response
            }

            inputCancellation.Cancel();
            try
            {
                if (streamResult.InputTask is not null)
                    await streamResult.InputTask;
            }
            catch (Exception ex)
            {
                streamResult.InputException = ex;
            }

            _pipelineStreamChannel.Close();
            inputCancellation.Dispose();
        }


        /// <summary>
        /// Sends the pipeline request, waits and returns the response
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>PipelineResponse.</returns>
        private async Task<PipelineResponse> SendPipelineRequest(PipelineRequest request, CancellationToken cancellationToken = default)
        {
            await _pipelineLock.WaitAsync(cancellationToken);
            try
            {
                await _pipelineChannel.SendMessage(request, cancellationToken);
                return await _pipelineChannel.ReceiveMessage<PipelineResponse>(cancellationToken);
            }
            finally
            {
                _pipelineLock.Release();
            }
        }


        /// <summary>
        /// Sends the command request, waits and returns the response
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>CommandResponse.</returns>
        private async Task<CommandResponse> SendCommandRequest(CommandRequest request, CancellationToken cancellationToken = default)
        {
            await _commandLock.WaitAsync(cancellationToken);
            try
            {
                await _commandChannel.SendMessage(request, cancellationToken);
                return await _commandChannel.ReceiveMessage<CommandResponse>(cancellationToken);
            }
            finally
            {
                _commandLock.Release();
            }
        }


        /// <summary>
        /// Sends the tensor response complete.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task SendTensorResponseComplete(CancellationToken cancellationToken = default)
        {
            var message = new CommandRequest(CommandRequestType.Complete);
            await SendCommandRequest(message, cancellationToken);
        }


        /// <summary>
        /// The state of a stream run.
        /// </summary>
        private sealed record StreamResult
        {
            public bool IsStarted { get; set; }
            public Task InputTask { get; set; }
            public Task<PipelineResponse> ResponseTask { get; set; }
            public PipelineResponse Response { get; set; }
            public Exception InputException { get; set; }
        }
    }
}
