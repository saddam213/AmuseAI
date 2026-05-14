using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Python;
using TensorStack.Python.Common;

namespace Amuse.Common
{
    public sealed class PipelineServer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _directoryBase;
        private readonly ChannelConfig _channelConfig;
        private readonly NamedPipeServerStream _commandChannel;
        private readonly NamedPipeServerStream _pipelineChannel;
        private readonly NamedPipeServerStream _progressChannel;
        private readonly Channel<PipelineProgress> _progressQueue;
        private readonly IProgress<PipelineProgress> _progressCallback;
        private CancellationTokenSource _pipelineCancellation;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineServer"/> class.
        /// </summary>
        /// <param name="serverConfig">The server configuration.</param>
        /// <param name="pipelineConfig">The pipeline configuration.</param>
        /// <param name="logger">The logger.</param>
        public PipelineServer(string directoryBase, ChannelConfig channelConfig, ILogger logger)
        {
            _logger = logger;
            _directoryBase = directoryBase;
            _channelConfig = channelConfig;
            _progressChannel = new NamedPipeServerStream(_channelConfig.ChannelProgress, PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, _channelConfig.ChunkSize, _channelConfig.ChunkSize);
            _commandChannel = new NamedPipeServerStream(_channelConfig.ChannelCommand, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, _channelConfig.ChunkSize, _channelConfig.ChunkSize);
            _pipelineChannel = new NamedPipeServerStream(_channelConfig.ChannelPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, _channelConfig.ChunkSize, _channelConfig.ChunkSize);
            _progressQueue = Channel.CreateUnbounded<PipelineProgress>();
            _progressCallback = new Progress<PipelineProgress>(async (p) => await _progressQueue.Writer.WriteAsync(p));
        }


        /// <summary>
        /// Start the Server loop
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await WaitForConnectionAsync(cancellationToken);

            _ = StartProgressChannelAsync(cancellationToken);
            _ = StartCommandChannelAsync(cancellationToken);
            await StartPipelineChannelAsync(cancellationToken);
            _logger.LogInformation($"[PipelineServer] [Start] Generate loop stopped.");
        }


        /// <summary>
        /// Wait for connection.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[PipelineServer] [WaitForConnection] Waiting for connection...");
            await Task.WhenAll
            (
                _progressChannel.WaitForConnectionAsync(cancellationToken),
                _commandChannel.WaitForConnectionAsync(cancellationToken),
                _pipelineChannel.WaitForConnectionAsync(cancellationToken)
            );
            _logger.LogInformation($"[PipelineServer] [WaitForConnection] Client connected.");
        }


        /// <summary>
        /// Start pipeline channel
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task StartPipelineChannelAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[PipelineServer] [PipelineChannel] Start pipeline channel.");

            var pipeline = default(PythonPipeline);
            var pipelineState = RequestType.Stop;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"[PipelineServer] [PipelineChannel] Waiting for request.");
                    var request = await _pipelineChannel.ReceiveMessage<PipelineRequest>(cancellationToken);

                    _logger.LogInformation($"[PipelineServer] [PipelineChannel] {request.Type} request received.");
                    if (request.Type == RequestType.Stop)
                    {
                        await StopServerAsync(request, cancellationToken);
                        pipelineState = RequestType.Stop;
                    }
                    else if (request.Type == RequestType.Start && pipelineState == RequestType.Stop)
                    {
                        await StartServerAsync(request, cancellationToken);
                        pipelineState = RequestType.Start;
                    }
                    else if (request.Type == RequestType.Environment && pipelineState == RequestType.Start)
                    {
                        await CreateEnvironmentAsync(request, cancellationToken);
                        pipelineState = RequestType.Environment;
                    }
                    else
                    {
                        if (pipelineState == RequestType.Environment)
                        {
                            if (request.Type == RequestType.PipelineDownload)
                            {
                                await DownloadPipelineAsync(request, cancellationToken);
                            }
                            else if (request.Type == RequestType.PipelineLoad)
                            {
                                pipeline = await LoadPipelineAsync(request, cancellationToken);
                            }
                            else if (request.Type == RequestType.PipelineReload)
                            {
                                await ReloadPipelineAsync(request, pipeline, cancellationToken);
                            }
                            else if (request.Type == RequestType.PipelineUnload)
                            {
                                await UnloadPipelineAsync(request, pipeline, cancellationToken);
                            }

                            else if (request.Type == RequestType.PipelineRun)
                            {
                                await RunPipelineAsync(request, pipeline, cancellationToken);
                            }
                        }
                    }

                    if (pipelineState == RequestType.Stop)
                        break;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PipelineServer] [PipelineChannel] An unexpected exception occurred");
                    break;
                }
            }

            pipeline?.Dispose();
            _logger.LogInformation($"[PipelineServer] [PipelineChannel] Pipeline channel closed.");
        }


        /// <summary>
        /// Start command channel
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task StartCommandChannelAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[PipelineServer] [CommandChannel] Start command channel.");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"[PipelineServer] [CommandChannel] Waiting for command...");
                    var commandMessage = await _commandChannel.ReceiveObject<CommandRequest>(cancellationToken);
                    if (commandMessage == null)
                        continue;

                    _logger.LogInformation("[PipelineServer] [CommandChannel] Received {Type} command.", commandMessage.Type);
                    if (commandMessage.Type == CommandRequestType.Cancel)
                        await _pipelineCancellation.SafeCancelAsync();

                    await _commandChannel.SendObject(new CommandResponse(), cancellationToken);
                    _logger.LogInformation("[PipelineServer] [CommandChannel] Processed {Type} command.", commandMessage.Type);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[PipelineServer] [CommandChannel] - An exception occurred receiving command.");
                    await _commandChannel.SendObject(new CommandResponse(ex), cancellationToken);
                }
            }
            _logger.LogInformation($"[PipelineServer] [CommandChannel] Close command channel.");
        }


        /// <summary>
        /// Process the progress queue
        /// </summary>
        /// <param name="progressQueue">The progress queue.</param>
        private async Task StartProgressChannelAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[PipelineServer] [ProgressChannel] Start progress channel.");
            await foreach (var progressMessage in _progressQueue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await _progressChannel.SendObject(progressMessage, cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[PipelineServer] [ProgressChannel] - An exception occurred processing progress.");
                }
            }
            _logger.LogInformation($"[PipelineServer] [ProgressChannel] Close progress channel.");
        }


        /// <summary>
        /// Start the server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task StartServerAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            await _pipelineChannel.SendResponse(cancellationToken);
            _logger.LogInformation($"[PipelineServer] [StartServer] Server started.");
        }


        /// <summary>
        /// Stop the server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task StopServerAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            await _pipelineChannel.SendResponse(cancellationToken);
            _logger.LogInformation($"[PipelineServer] [StopServer] Server stopped.");
        }


        /// <summary>
        /// Create environment
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task CreateEnvironmentAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var timestamp = Stopwatch.GetTimestamp();
                var environmentRequest = request.Environment;
                var pythonEnvironment = new PythonManager(environmentRequest.Config, _directoryBase, _logger);
                if(environmentRequest.Mode == EnvironmentMode.Load || (environmentRequest.Mode == EnvironmentMode.Create && pythonEnvironment.Exists()))
                {
                    _logger.LogInformation("[PipelineServer] [CreateEnvironment] Loading existing environment...");
                    await pythonEnvironment.LoadAsync(_progressCallback);
                }
                else
                {
                    _logger.LogInformation($"[PipelineServer] [CreateEnvironment] Creating environment, Mode: {environmentRequest.Mode}...");
                    await pythonEnvironment.CreateAsync(environmentRequest.Mode, _progressCallback);
                }

                _logger.LogInformation($"[PipelineServer] [CreateEnvironment] Environment created, Elapsed: {Stopwatch.GetElapsedTime(timestamp)}");
                await _pipelineChannel.SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [CreateEnvironment] An exception occurred creating environment.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
            }
        }


        /// <summary>
        /// Loads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<PythonPipeline> LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var pipeline = new PythonPipeline(request.PipelineConfig, _progressCallback, _logger);
                await pipeline.LoadAsync();
                await _pipelineChannel.SendResponse(cancellationToken);
                return pipeline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
                return default;
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<PythonPipeline> ReloadPipelineAsync(PipelineRequest request, PythonPipeline pipeline, CancellationToken cancellationToken)
        {
            try
            {
                await pipeline.ReloadAsync(request.PipelineReloadOptions);
                await _pipelineChannel.SendResponse(cancellationToken);
                return pipeline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [ReloadPipeline] An exception occurred reloading pipeline.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
                return default;
            }
        }


        /// <summary>
        /// Unloads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task UnloadPipelineAsync(PipelineRequest request, PythonPipeline pipeline, CancellationToken cancellationToken)
        {
            try
            {
                await pipeline.UnloadAsync();
                await _pipelineChannel.SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [UnloadPipeline] An exception occurred unloading pipeline.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
            }
        }


        /// <summary>
        /// Download the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task DownloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var pipeline = new PythonPipeline(request.PipelineConfig, _progressCallback, _logger);
                await pipeline.DownloadAsync();
                await _pipelineChannel.SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [DownloadPipeline] An exception occurred unloading pipeline.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
            }
        }


        /// <summary>
        /// Runs the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task RunPipelineAsync(PipelineRequest request, PythonPipeline pipeline, CancellationToken cancellationToken)
        {
            try
            {
                _pipelineCancellation = new CancellationTokenSource();
                RehydratePipelineOptions(request);
                var response = await pipeline.GenerateAsync(request.PipelineOptions, _pipelineCancellation.Token);
                await _pipelineChannel.SendMessage(new PipelineResponse(response));
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError("[PipelineServer] [RunPipeline] {Message}", ex.Message);
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PipelineServer] [RunPipeline] An exception occurred running pipeline.");
                await _pipelineChannel.SendMessage(new PipelineResponse(ex), cancellationToken);
            }
            finally
            {
                _pipelineCancellation.Dispose();
                _pipelineCancellation = null;
            }
        }


        /// <summary>
        /// Rehydrates the pipeline options.
        /// </summary>
        /// <param name="pipelineRequest">The pipeline request.</param>
        private void RehydratePipelineOptions(PipelineRequest pipelineRequest)
        {
            if (pipelineRequest.ImageTensorCount > 0)
            {
                pipelineRequest.PipelineOptions.InputImages = pipelineRequest.Tensors
                    .Take(pipelineRequest.ImageTensorCount)
                    .Select(x => x.AsImageTensor())
                    .ToList();
            }

            if (pipelineRequest.ControlNetTensorCount > 0)
            {
                pipelineRequest.PipelineOptions.InputControlImages = pipelineRequest.Tensors
                    .Skip(pipelineRequest.ImageTensorCount)
                    .Take(pipelineRequest.ControlNetTensorCount)
                    .Select(x => x.AsImageTensor())
                    .ToList();
            }

            if (pipelineRequest.AudioTensorCount > 0)
            {
                pipelineRequest.PipelineOptions.InputAudios = pipelineRequest.Tensors
                    .Take(pipelineRequest.AudioTensorCount)
                    .Select(x => x.AsAudioTensor(pipelineRequest.AudioSampleRate))
                    .ToList();
            }

        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _pipelineCancellation?.SafeCancel();
            _pipelineCancellation?.Dispose();
            _progressChannel?.Dispose();
            _commandChannel?.Dispose();
            _pipelineChannel?.Dispose();
        }

    }
}
