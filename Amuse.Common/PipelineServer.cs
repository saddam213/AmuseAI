using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public abstract class PipelineServer : IDisposable
    {
        private readonly NamedPipeServerStream _commandChannel;
        private readonly NamedPipeServerStream _pipelineChannel;
        private readonly NamedPipeServerStream _progressChannel;
        private readonly Channel<PipelineProgress> _progressQueue;
        private RequestType _pipelineState;
        private MemoryMappedFile _tensorChannel;
        private PipelineTensorChannel _pipelineTensorChannel;


        public PipelineServer(ServerConfig config, ILogger logger)
        {
            Logger = logger;
            Config = config;
            _progressQueue = Channel.CreateUnbounded<PipelineProgress>();
            _progressChannel = new NamedPipeServerStream(Config.ChannelProgress, PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, Config.ChunkSize, Config.ChunkSize);
            _commandChannel = new NamedPipeServerStream(Config.ChannelCommand, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, Config.ChunkSize, Config.ChunkSize);
            _pipelineChannel = new NamedPipeServerStream(Config.ChannelPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, Config.ChunkSize, Config.ChunkSize);
            _pipelineTensorChannel = new PipelineTensorChannel(Config);
        }

        protected ILogger Logger { get; }
        protected ServerConfig Config { get; }
        protected CancellationTokenSource PipelineCancellation { get; set; }
        protected RequestType PipelineState => _pipelineState;


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
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [Start] Generate loop stopped.");
        }


        /// <summary>
        /// Wait for connection.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [WaitForConnection] Waiting for connection...");
            await Task.WhenAll
            (
                _progressChannel.WaitForConnectionAsync(cancellationToken),
                _commandChannel.WaitForConnectionAsync(cancellationToken),
                _pipelineChannel.WaitForConnectionAsync(cancellationToken)
            );
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [WaitForConnection] Client connected.");
        }


        /// <summary>
        /// Start pipeline channel
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task StartPipelineChannelAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [PipelineChannel] Start pipeline channel.");

            _pipelineState = RequestType.Stop;
            await ChannelOpenedAsync();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Logger.LogInformation($"[AmuseHost] [PipelineServer] [PipelineChannel] Waiting for request.");
                    var request = await _pipelineChannel.ReceiveMessage<PipelineRequest>(cancellationToken);

                    Logger.LogInformation($"[AmuseHost] [PipelineServer] [PipelineChannel] {request.Type} request received.");
                    if (request.Type == RequestType.Stop)
                    {
                        await StopServerAsync(request, cancellationToken);
                        _pipelineState = RequestType.Stop;
                    }
                    else if (request.Type == RequestType.Start && _pipelineState == RequestType.Stop)
                    {
                        await StartServerAsync(request, cancellationToken);
                        _pipelineState = RequestType.Start;
                    }
                    else if (request.Type == RequestType.Create && _pipelineState == RequestType.Start)
                    {
                        await CreatePipelineAsync(request, cancellationToken);
                        _pipelineState = RequestType.Create;
                    }
                    else
                    {
                        if (_pipelineState == RequestType.Create)
                        {
                            if (request.Type == RequestType.Load)
                            {
                                await LoadPipelineAsync(request, cancellationToken);
                            }
                            else if (request.Type == RequestType.Reload)
                            {
                                await ReloadPipelineAsync(request, cancellationToken);
                            }
                            else if (request.Type == RequestType.Unload)
                            {
                                await UnloadPipelineAsync(request, cancellationToken);
                            }
                            else if (request.Type == RequestType.Run)
                            {
                                await RunPipelineAsync(request, cancellationToken);
                            }
                        }
                    }

                    if (_pipelineState == RequestType.Stop)
                        break;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[AmuseHost] [PipelineServer] [PipelineChannel] An unexpected exception occurred");
                    break;
                }
            }

            await ChannelClosedAsync();
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [PipelineChannel] Pipeline channel closed.");
        }


        /// <summary>
        /// Start command channel
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task StartCommandChannelAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [CommandChannel] Start command channel.");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Logger.LogInformation($"[AmuseHost] [PipelineServer] [CommandChannel] Waiting for command...");
                    var commandMessage = await _commandChannel.ReceiveMessage<CommandRequest>(cancellationToken);
                    if (commandMessage == null)
                        continue;

                    Logger.LogInformation("[AmuseHost] [PipelineServer] [CommandChannel] Received {Type} command.", commandMessage.Type);
                    if (commandMessage.Type == CommandRequestType.Cancel)
                        await PipelineCancellation.SafeCancelAsync();
                    else if (commandMessage.Type == CommandRequestType.Complete)
                        await CloseTensorChannelAsync();

                    await _commandChannel.SendMessage(new CommandResponse(), cancellationToken);
                    Logger.LogInformation("[AmuseHost] [PipelineServer] [CommandChannel] Processed {Type} command.", commandMessage.Type);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"[AmuseHost] [PipelineServer] [CommandChannel] - An exception occurred receiving command.");
                    await _commandChannel.SendMessage(new CommandResponse(ex), cancellationToken);
                }
            }
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [CommandChannel] Close command channel.");
        }


        /// <summary>
        /// Process the progress queue
        /// </summary>
        /// <param name="progressQueue">The progress queue.</param>
        protected async Task StartProgressChannelAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [ProgressChannel] Start progress channel.");
            await foreach (var progressMessage in _progressQueue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await _progressChannel.SendTensorMessage(progressMessage, cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"[AmuseHost] [PipelineServer] [ProgressChannel] - An exception occurred processing progress.");
                }
            }
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [ProgressChannel] Close progress channel.");
        }


        /// <summary>
        /// Start the server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task StartServerAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            await SendResponse(cancellationToken);
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [StartServer] Server started.");
        }


        /// <summary>
        /// Stop the server
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task StopServerAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            await SendResponse(cancellationToken);
            Logger.LogInformation($"[AmuseHost] [PipelineServer] [StopServer] Server stopped.");
        }


        protected async Task SendResponse<T>(T message, CancellationToken cancellationToken)
        {
            await _pipelineChannel.SendMessage(message, cancellationToken);
        }

        protected Task SendResponse(CancellationToken cancellationToken = default)
        {
            return _pipelineChannel.SendMessage(new PipelineResponse(), cancellationToken);
        }


        protected async Task SendException(Exception exception, CancellationToken cancellationToken)
        {
            await _pipelineChannel.SendMessage(new PipelineResponse(exception), cancellationToken);
        }


        protected async Task SendTensorResponse(CancellationToken cancellationToken, params ImageTensor[] image)
        {
            var response = new PipelineResponse(image);
            var packedTensors = response.PackTensors();
            _tensorChannel = _pipelineTensorChannel.WriteResponse(packedTensors);
            await _pipelineChannel.SendMessage(response, cancellationToken);
        }


        protected async Task SendTensorResponse(CancellationToken cancellationToken, params AudioTensor[] audio)
        {
            var response = new PipelineResponse(audio);
            var packedTensors = response.PackTensors();
            _tensorChannel = _pipelineTensorChannel.WriteResponse(packedTensors);
            await _pipelineChannel.SendMessage(response, cancellationToken);
        }


        protected async Task SendTensorResponse(CancellationToken cancellationToken, params VideoSequence[] video)
        {
            var response = new PipelineResponse(video);
            var packedTensors = response.PackTensors();
            _tensorChannel = _pipelineTensorChannel.WriteResponse(packedTensors);
            await _pipelineChannel.SendMessage(response, cancellationToken);
        }


        protected async Task SendTensorResponse(CancellationToken cancellationToken, params TextInput[] text)
        {
            var response = new PipelineResponse(text);
            _tensorChannel = _pipelineTensorChannel.WriteResponse([]);
            await _pipelineChannel.SendMessage(response, cancellationToken);
        }


        protected void ReadTensorRequest(PipelineRequest request)
        {
            _pipelineTensorChannel.ReadRequest(request);
        }


        protected Task CloseTensorChannelAsync()
        {
            _tensorChannel?.Dispose();
            _tensorChannel = null;
            return Task.CompletedTask;
        }


        protected async Task SendTensorResponse(PipelineResponse response, CancellationToken cancellationToken)
        {
            var packedTensors = response.PackTensors();
            _tensorChannel = _pipelineTensorChannel.WriteResponse(packedTensors);
            await _pipelineChannel.SendMessage(response, cancellationToken);
        }


        protected async Task QueueProgress(PipelineProgress progress)
        {
            await _progressQueue.Writer.WriteAsync(progress);
        }


        /// <summary>
        /// Queues the progress synchronously, the queue is unbounded so the write always succeeds and reports keep their order.
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected void EnqueueProgress(PipelineProgress progress)
        {
            _progressQueue.Writer.TryWrite(progress);
        }


        /// <summary>
        /// Called when the Channel is opened.
        /// </summary>
        /// <returns>Task.</returns>
        protected abstract Task ChannelOpenedAsync();


        /// <summary>
        /// Called when the Channel is closed.
        /// </summary>
        /// <returns>Task.</returns>
        protected abstract Task ChannelClosedAsync();


        /// <summary>
        /// Create Pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task CreatePipelineAsync(PipelineRequest request, CancellationToken cancellationToken);


        /// <summary>
        /// Loads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken);


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken);


        /// <summary>
        /// Unloads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task UnloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken);


        /// <summary>
        /// Runs the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task RunPipelineAsync(PipelineRequest request, CancellationToken cancellationToken);


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            PipelineCancellation?.SafeCancel();
            PipelineCancellation?.Dispose();
            _progressChannel?.Dispose();
            _commandChannel?.Dispose();
            _pipelineChannel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
