using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.StableDiffusionCpp;
using PipelineProgress = Amuse.Common.PipelineProgress;
using StableDiffusionOptions = TensorStack.StableDiffusionCpp.Common.StableDiffusionOptions;

namespace Amuse.Host.StableDiffusionCpp
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<IRunProgress> _progressCallback;
        private PipelineCreateOptions _options;
        private PipelineLoadOptions _pipelineOptions;
        private StableDiffusionOptions _contextOptions;
        private StableDiffusionPipeline _pipeline;

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
                var configuration = new TensorStack.Common.BackendConfig
                {
                    Name = request.CreateOptions.Environment,
                    Requirements = request.CreateOptions.Requirements,
                    Directory = request.CreateOptions.Directory
                };

                var installManager = new StableDiffusionBackend(configuration, _progressCallback, Logger);
                if (!await installManager.SetupAsync(request.CreateOptions.Status))
                    throw new Exception($"Failed to Install StableDiffusion.Cpp {request.CreateOptions.HostVersion}");

                _options = request.CreateOptions;
                Logger.LogInformation($"[AmuseHost] [PipelineServer] [CreatePipeline] Environment created, Elapsed: {Stopwatch.GetElapsedTime(timestamp)}");
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
                _contextOptions = null;

                var backendDirectory = Path.Combine(_options.Directory, _options.Environment);
                _pipeline = new StableDiffusionPipeline(backendDirectory, _progressCallback, Logger);
                if (_pipelineOptions.IsUpscalePipeline())
                {
                    // Upscale
                    var upscaleOptions = _options.CreateUpscaleOptions(_pipelineOptions)
                        ?? throw new Exception("Upscale pipeline requires an upscale configuration.");
                    await _pipeline.LoadAsync(upscaleOptions, cancellationToken);
                }
                else
                {
                    // Diffusion
                    var contextOptions = _options.CreateContextOptions(_pipelineOptions);
                    await _pipeline.LoadAsync(contextOptions, cancellationToken: cancellationToken);
                    _contextOptions = contextOptions;
                }

                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _pipeline?.Dispose();
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Reload the pipeline.
        /// LoRAs and the process type apply per run, a change to the context options such as the ControlNet reloads the model configuration and keeps the unchanged components loaded.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                var timestamp = Stopwatch.GetTimestamp();
                var reloadOptions = request.ReloadOptions;
                _pipelineOptions.LoraAdapters = reloadOptions.LoraAdapters;
                _pipelineOptions.ProcessType = reloadOptions.ProcessType;
                _pipelineOptions.ControlNet = reloadOptions.ControlNet;
                if (_contextOptions != null)
                {
                    var contextOptions = _options.CreateContextOptions(_pipelineOptions);
                    if (!contextOptions.Equals(_contextOptions))
                    {
                        await _pipeline.ReloadAsync(contextOptions, cancellationToken);
                        _contextOptions = contextOptions;
                        Logger.LogInformation("[AmuseHost] [PipelineServer] [ReloadPipeline] Context options reloaded, ControlNet: {ControlNet}, Elapsed: {Elapsed:c}", contextOptions.ControlNetPath, Stopwatch.GetElapsedTime(timestamp));
                    }
                }
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
                _pipeline.Unload();
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

                    if (request.RunOptions.UpscaleImageOptions != null)
                    {
                        // Upscale Image
                        ThrowIfNotUpscalePipeline();
                        var options = request.RunOptions.UpscaleImageOptions;
                        var inputImage = options.InputImages.FirstOrDefault() ?? throw new Exception("No input image to upscale.");
                        var imageResult = await _pipeline.UpscaleImageAsync(inputImage, cancellationToken: PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, imageResult);
                    }
                    else if (request.RunOptions.UpscaleVideoOptions != null)
                    {
                        // Upscale Video
                        ThrowIfNotUpscalePipeline();
                        var options = request.RunOptions.UpscaleVideoOptions;
                        var inputVideo = options.InputVideos.FirstOrDefault() ?? throw new Exception("No input video to upscale.");
                        var upscaleOptions = Extensions.CreateUpscaleVideoOptions(options.Seed, options.WindowSize, options.TemporalChunkSize);
                        var videoResult = await _pipeline.UpscaleVideoAsync(inputVideo, upscaleOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, videoResult);
                    }
                    else if (request.RunOptions.ImageOptions != null)
                    {
                        ThrowIfUpscalePipeline();
                        var options = request.RunOptions.ImageOptions;
                        var generateOptions = _pipeline.DefaultImageOptions.CreateImageOptions(options, _pipelineOptions);
                        var imageResult = await _pipeline.GenerateImageAsync(generateOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, imageResult);
                    }
                    else if (request.RunOptions.VideoOptions != null)
                    {
                        ThrowIfUpscalePipeline();
                        var options = request.RunOptions.VideoOptions;
                        var generateVideoOptions = _pipeline.DefaultVideoOptions.CreateVideoOptions(options, _pipelineOptions);
                        if (options.IsChunkedVideo(_pipelineOptions))
                        {
                            var generator = new ChunkedVideoGenerator(_pipeline, VideoFrameRule.Get(_pipelineOptions.Pipeline), SendChunkProgress, Logger);
                            await generator.GenerateAsync(generateVideoOptions, new ChunkOptions(options.FrameChunk, options.EnableFrameChunkColorMatch, options.FrameChunkStrength), options.TempFileName, PipelineCancellation.Token);
                            await SendTensorResponse(cancellationToken, Array.Empty<VideoSequence>());
                        }
                        else
                        {
                            var singleVideoOptions = generateVideoOptions with
                            {
                                Prompt = ChunkPrompt.GetPrompt(generateVideoOptions.Prompt, 1),
                                NegativePrompt = ChunkedVideoGenerator.GetNegativePrompt(generateVideoOptions.NegativePrompt, 1)
                            };
                            var videoResult = await _pipeline.GenerateVideoAsync(singleVideoOptions, PipelineCancellation.Token);
                            await SendTensorResponse(cancellationToken, videoResult);
                        }
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
        /// Runs the pipeline as a stream, items are processed as they arrive from the client.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="input">The items streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override IAsyncEnumerable<PipelineStreamItem> RunPipelineStreamAsync(PipelineRequest request, IAsyncEnumerable<PipelineStreamItem> input, CancellationToken cancellationToken)
        {
            if (request.RunOptions.UpscaleImageOptions != null)
                return UpscaleStreamAsync(input, cancellationToken);

            throw new NotSupportedException("The stream request is not supported by StableDiffusionCpp.");
        }


        /// <summary>
        /// Upscales the video frames streamed from the client with the image upscaler, returning each frame as it is upscaled.
        /// </summary>
        /// <param name="input">The frames streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async IAsyncEnumerable<PipelineStreamItem> UpscaleStreamAsync(IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ThrowIfNotUpscalePipeline();
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                var inputImage = item.Tensors[0].AsImageTensor();
                var imageResult = await _pipeline.UpscaleImageAsync(inputImage, cancellationToken: cancellationToken);
                yield return item with { Tensors = [imageResult] };
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
        /// Sends the progress of a chunked video as each chunk starts.
        /// </summary>
        /// <param name="chunk">The chunk number.</param>
        /// <param name="chunkCount">The chunk count.</param>
        private void SendChunkProgress(int chunk, int chunkCount)
        {
            EnqueueProgress(new PipelineProgress { Key = "Generate", Subkey = "Chunk", BatchValue = chunk, BatchMaximum = chunkCount });
        }


        /// <summary>
        /// Throws if the loaded pipeline is not an upscale pipeline.
        /// </summary>
        private void ThrowIfNotUpscalePipeline()
        {
            if (_pipeline == null || !_pipelineOptions.IsUpscalePipeline())
                throw new Exception("Upscale requires an upscale pipeline to be loaded.");
        }


        /// <summary>
        /// Throws if the loaded pipeline is an upscale-only pipeline.
        /// </summary>
        private void ThrowIfUpscalePipeline()
        {
            if (_pipeline == null || _pipelineOptions.IsUpscalePipeline())
                throw new Exception("Generate requires a diffusion pipeline to be loaded.");
        }


        /// <summary>
        /// Sends loading the progress.
        /// </summary>
        private async Task SendLoadingProgress(string message = null)
        {
            await QueueProgress(new PipelineProgress { Key = "Load", Subkey = "Pipeline", Message = message ?? "Loading Pipeline Components..." });
        }
    }
}