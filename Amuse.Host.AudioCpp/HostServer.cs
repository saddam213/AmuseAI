using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.AudioCpp;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using PipelineProgress = Amuse.Common.PipelineProgress;

namespace Amuse.Host.AudioCpp
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<IRunProgress> _progressCallback;
        private PipelineCreateOptions _options;
        private PipelineLoadOptions _pipelineOptions;
        private AudioCppPipeline _pipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostServer"/> class.
        /// </summary>
        /// <param name="channelConfig">The channel configuration.</param>
        /// <param name="logger">The logger.</param>
        public HostServer(ServerConfig channelConfig, ILogger logger)
            : base(channelConfig, logger)
        {
            _progressCallback = new SyncProgress<IRunProgress>(UpdateProgress);
        }


        /// <summary>
        /// Called when the Channel is opened.
        /// </summary>
        /// <returns>Task.</returns>
        protected override Task ChannelOpenedAsync()
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when the Channel is closed.
        /// </summary>
        protected override Task ChannelClosedAsync()
        {
            if (_pipeline != null)
                _pipeline.Dispose();

            return Task.CompletedTask;
        }


        /// <summary>
        /// Create Pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task CreatePipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress("Creating Environment...");

                var timestamp = Stopwatch.GetTimestamp();
                var configuration = new BackendConfig
                {
                    Name = request.CreateOptions.Environment,
                    Requirements = request.CreateOptions.Requirements,
                    Directory = request.CreateOptions.Directory
                };

                var installManager = new AudioCppBackend(configuration, _progressCallback, Logger);
                if (!await installManager.SetupAsync(request.CreateOptions.Status))
                    throw new Exception($"Failed to Install audio.cpp {request.CreateOptions.HostVersion}");

                _options = request.CreateOptions;
                Logger.LogInformation("[AmuseHost] [PipelineServer] [CreatePipeline] Environment created, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [CreatePipeline] An exception occurred creating environment.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Loads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                _pipelineOptions = request.LoadOptions;

                var loadOptions = _options.CreateLoadOptions(_pipelineOptions);
                var backendDirectory = Path.Combine(_options.Directory, _options.Environment);
                _pipeline ??= new AudioCppPipeline(backendDirectory, Logger);
                await _pipeline.LoadAsync(loadOptions, cancellationToken);

                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _pipeline?.Unload();
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                var reloadOptions = request.ReloadOptions;
                _pipelineOptions.LoraAdapters = reloadOptions.LoraAdapters;
                _pipelineOptions.ProcessType = reloadOptions.ProcessType;
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [ReloadPipeline] An exception occurred reloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Unloads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task UnloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _pipeline?.Unload();
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [UnloadPipeline] An exception occurred unloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Runs the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task RunPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                using (PipelineCancellation = new CancellationTokenSource())
                {
                    ReadTensorRequest(request);

                    if (request.RunOptions.AudioOptions != null)
                    {
                        // Audio
                        var audioOptions = request.RunOptions.AudioOptions.CreateAudioOptions(_pipelineOptions, Logger);
                        await SendGenerateProgress();
                        var audioResult = await _pipeline.GenerateAudioAsync(audioOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, audioResult);
                    }
                    else if (request.RunOptions.TextOptions != null)
                    {
                        // Text
                        var textOptions = request.RunOptions.TextOptions.CreateTextOptions();
                        await SendGenerateProgress();
                        var textResult = await _pipeline.GenerateTextAsync(textOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, new TextInput(textResult));
                    }
                    else
                    {
                        throw new NotSupportedException("audio.cpp only supports audio and text requests.");
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogInformation("[AmuseHost] [PipelineServer] [RunPipeline] {Message}", ex.Message);
                await SendException(ex, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [RunPipeline] An exception occurred running pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Runs a pipeline stream, live speech recognition takes audio chunks and returns the text as it is recognised.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="input">The items streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override IAsyncEnumerable<PipelineStreamItem> RunPipelineStreamAsync(PipelineRequest request, IAsyncEnumerable<PipelineStreamItem> input, CancellationToken cancellationToken)
        {
            if (request.RunOptions.TextOptions != null)
                return GenerateTextStreamAsync(request.RunOptions.TextOptions, input, cancellationToken);

            throw new NotSupportedException("The stream request is not supported by audio.cpp.");
        }


        /// <summary>
        /// Recognises the audio chunks streamed from the client, returning the text as it is recognised.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="input">The audio chunks streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async IAsyncEnumerable<PipelineStreamItem> GenerateTextStreamAsync(GenerateTextOptions options, IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_pipeline == null || !_pipeline.IsLoaded)
                throw new Exception("Live speech recognition requires a model to be loaded.");

            var index = 0;
            var timestamp = Stopwatch.GetTimestamp();
            var textOptions = options.CreateTextOptions();
            var inputAudio = CreateAudioChunksAsync(input, cancellationToken);
            await foreach (var text in _pipeline.GenerateTextStreamAsync(textOptions, inputAudio, cancellationToken))
            {
                yield return new PipelineStreamItem { Index = index++, Text = text };
            }
            Logger.LogInformation("[AmuseHost] [PipelineServer] [RunStream] Text stream complete, Results: {Results}, Elapsed: {Elapsed:c}", index, Stopwatch.GetElapsedTime(timestamp));
        }


        /// <summary>
        /// Converts the stream items to audio chunks as the pipeline takes them.
        /// </summary>
        /// <param name="input">The items streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<AudioTensor> CreateAudioChunksAsync(IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                if (item.Tensors.Count == 0)
                    continue;

                yield return new AudioTensor(item.Tensors[0], (int)item.Rate);
            }
        }


        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        private void UpdateProgress(IRunProgress progress)
        {
            if (progress is TensorStack.Common.Pipeline.PipelineProgress pipelineProgress)
            {
                EnqueueProgress(pipelineProgress.ToProgress());
            }
        }


        /// <summary>
        /// Sends loading the progress.
        /// </summary>
        /// <param name="message">The message.</param>
        private async Task SendLoadingProgress(string message = null)
        {
            await QueueProgress(new PipelineProgress { Key = "Load", Subkey = "Pipeline", Message = message ?? "Loading Pipeline Components..." });
        }


        /// <summary>
        /// Sends the generate progress, audio.cpp does not report step progress through its C ABI so this is indeterminate.
        /// </summary>
        private async Task SendGenerateProgress()
        {
            await SendLoadingProgress("Generating...");
        }
    }
}
