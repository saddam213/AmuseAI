using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
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
using TensorStack.Common.Video;
using TensorStack.OnnxRuntime;
using TensorStack.OnnxRuntime.Extract;
using TensorStack.OnnxRuntime.Upscale;
using PipelineProgress = Amuse.Common.PipelineProgress;


namespace Amuse.Host.OnnxRuntime
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<RunProgress> _progressRelayRunCallback;

        private IPipeline _pipeline;
        private PipelineLoadOptions _pipelineOptions;
        private ExecutionProvider _executionProvider;

        public HostServer(ServerConfig channelConfig, ILogger logger)
            : base(channelConfig, logger)
        {
            _progressRelayRunCallback = new SyncProgress<RunProgress>(UpdateProgress);
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
            _pipeline?.Dispose();
            return Task.CompletedTask;
        }


        protected override async Task CreatePipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var timestamp = Stopwatch.GetTimestamp();
                var environmentRequest = request.CreateOptions;

                Logger.LogInformation($"[PipelineServer] [CreatePipeline] Environment created, Elapsed: {Stopwatch.GetElapsedTime(timestamp)}");
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [CreatePipeline] An exception occurred creating environment.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _pipelineOptions = request.LoadOptions;
                if (_pipelineOptions.Pipeline == PipelineType.UpscalePipeline)
                {
                    // Upscale
                    _pipeline = CreateUpscalePipeline(_pipelineOptions.UpscaleConfig);
                    await _pipeline.LoadAsync(cancellationToken);
                    await SendResponse(cancellationToken);
                    return;
                }

                if (_pipelineOptions.Pipeline == PipelineType.ExtractPipeline)
                {
                    // Extract
                    _pipeline = CreateExtractPipeline(_pipelineOptions.ExtractConfig);
                    await _pipeline.LoadAsync(cancellationToken);
                    await SendResponse(cancellationToken);
                    return;
                }

                if (_pipelineOptions.Pipeline == PipelineType.InterpolationPipeline)
                {
                    _pipeline = CreateInterpolationPipeline(_pipelineOptions.InterpolateConfig);
                    await _pipeline.LoadAsync(cancellationToken);
                    await SendResponse(cancellationToken);
                    return;
                }

                throw new NotSupportedException($"The {_pipelineOptions.Pipeline} is not supported by OnnxRuntime.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var reloadOptions = request.ReloadOptions;
                _pipelineOptions.ProcessType = reloadOptions.ProcessType;
                _pipelineOptions.ControlNet = reloadOptions.ControlNet;
                _pipelineOptions.LoraAdapters = reloadOptions.LoraAdapters;

                // TODO: Reload?

                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [ReloadPipeline] An exception occurred reloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task UnloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await _pipeline.UnloadAsync();
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [UnloadPipeline] An exception occurred unloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task RunPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                ReadTensorRequest(request);
                if (request.RunOptions.UpscaleImageOptions != null)
                {
                    using (PipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        var resultTensor = await UpscaleImageAsync(request.RunOptions.UpscaleImageOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, resultTensor);
                    }
                }
                else if (request.RunOptions.UpscaleVideoOptions != null)
                {
                    throw new NotSupportedException("Video upscale pipelines are not supported by OnnxRuntime.");
                }
                else if (request.RunOptions.ExtractImageOptions != null)
                {
                    using (PipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        var resultTensor = await ExtractImageAsync(request.RunOptions.ExtractImageOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, resultTensor);
                    }
                }
                else
                {
                    throw new NotSupportedException("The run request is not supported by OnnxRuntime.");
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogError("[PipelineServer] [RunPipeline] {Message}", ex.Message);
                await SendException(ex, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [RunPipeline] An exception occurred running pipeline.");
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
            if (request.RunOptions.InterpolateVideoOptions != null)
                return InterpolateVideoAsync(request.RunOptions.InterpolateVideoOptions, input, cancellationToken);
            if (request.RunOptions.UpscaleImageOptions != null)
                return UpscaleStreamAsync(request.RunOptions.UpscaleImageOptions, input, cancellationToken);
            if (request.RunOptions.ExtractImageOptions != null)
                return ExtractStreamAsync(request.RunOptions.ExtractImageOptions, input, cancellationToken);

            throw new NotSupportedException("The stream request is not supported by OnnxRuntime.");
        }


        /// <summary>
        /// Creates the interpolation pipeline.
        /// </summary>
        /// <param name="interpolateConfig">The interpolate configuration.</param>
        /// <returns>InterpolationPipeline.</returns>
        private InterpolationPipeline CreateInterpolationPipeline(InterpolateConfig interpolateConfig)
        {
            if (interpolateConfig == null)
                throw new Exception("Interpolation pipeline requires an interpolate configuration.");

            _executionProvider = interpolateConfig.DeviceType == DeviceType.CPU
                ? Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_ENABLE_ALL)
                : Provider.GetProvider(DeviceType.GPU, _pipelineOptions.DeviceId, GraphOptimizationLevel.ORT_ENABLE_ALL);

            return InterpolationPipeline.Create(new ModelConfig
            {
                ExecutionProvider = _executionProvider,
                Path = Path.Combine(AppContext.BaseDirectory, "Interpolation.onnx")
            });
        }


        /// <summary>
        /// Interpolates the video frames streamed from the client, returning the frames as they are produced.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="input">The frames streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async IAsyncEnumerable<PipelineStreamItem> InterpolateVideoAsync(InterpolateVideoRunOptions options, IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_pipeline is not InterpolationPipeline interpolationPipeline)
                throw new Exception("Interpolation requires an interpolation pipeline to be loaded.");

            var interpolationOptions = new InterpolationStreamOptions
            {
                Multiplier = options.Multiplier,
                FrameCount = options.FrameCount,
                FrameRate = options.FrameRate,
                Stream = ReadVideoFramesAsync(input, cancellationToken)
            };

            await foreach (var frame in interpolationPipeline.RunAsync(interpolationOptions, _progressRelayRunCallback, cancellationToken))
            {
                yield return frame.ToStreamItem();
            }
        }


        /// <summary>
        /// Extracts the video frames streamed from the client, returning each frame as it is extracted.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="input">The frames streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private IAsyncEnumerable<PipelineStreamItem> ExtractStreamAsync(ExtractImageRunOptions options, IAsyncEnumerable<PipelineStreamItem> input, CancellationToken cancellationToken)
        {
            var inputFrames = ReadVideoFramesAsync(input, cancellationToken);
            var resultFrames = _pipeline switch
            {
                ExtractorPipeline extractorPipeline => extractorPipeline.RunAsync(new ExtractorStreamOptions
                {
                    Stream = inputFrames,
                    IsInverted = options.IsInverted,
                    IsTileEnabled = options.IsTileEnabled,
                    MaxTileSize = options.TileSize,
                    TileOverlap = options.TileOverlap,
                    MergeInput = options.MergeInput
                }, _progressRelayRunCallback, cancellationToken),
                PosePipeline posePipeline => posePipeline.RunAsync(new PoseStreamOptions
                {
                    Stream = inputFrames,
                    Detections = options.Detections,
                    BodyConfidence = options.BodyConfidence,
                    JointConfidence = options.JointConfidence,
                    ColorAlpha = options.ColorAlpha,
                    JointRadius = options.JointRadius,
                    BoneRadius = options.BoneRadius,
                    BoneThickness = options.BoneThickness,
                    IsTransparent = options.IsTransparent
                }, _progressRelayRunCallback, cancellationToken),
                BackgroundPipeline backgroundPipeline => ExtractBackgroundStreamAsync(backgroundPipeline, options, inputFrames, cancellationToken),
                _ => throw new Exception("Extract requires an extract pipeline to be loaded.")
            };
            return CreateStreamItemsAsync(resultFrames, cancellationToken);
        }


        /// <summary>
        /// Extracts the background of each video frame, the background pipeline has no stream run so the frames are processed one at a time.
        /// </summary>
        /// <param name="backgroundPipeline">The background pipeline.</param>
        /// <param name="options">The options.</param>
        /// <param name="inputFrames">The input frames.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async IAsyncEnumerable<VideoFrame> ExtractBackgroundStreamAsync(BackgroundPipeline backgroundPipeline, ExtractImageRunOptions options, IAsyncEnumerable<VideoFrame> inputFrames, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var frame in inputFrames.WithCancellation(cancellationToken))
            {
                var backgroundOptions = new BackgroundImageOptions
                {
                    Image = frame.Frame,
                    Mode = (TensorStack.OnnxRuntime.Extract.BackgroundMode)options.Mode,
                    IsTransparentSupported = options.IsTransparentSupported
                };
                var resultImage = await Task.Run(() => backgroundPipeline.RunAsync(backgroundOptions, _progressRelayRunCallback, cancellationToken), cancellationToken);
                yield return new VideoFrame(frame.Index, resultImage, frame.SourceFrameRate);
            }
        }


        /// <summary>
        /// Converts the video frames to stream items.
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<PipelineStreamItem> CreateStreamItemsAsync(IAsyncEnumerable<VideoFrame> frames, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var frame in frames.WithCancellation(cancellationToken))
            {
                yield return frame.ToStreamItem();
            }
        }


        /// <summary>
        /// Reads the stream items as video frames.
        /// </summary>
        /// <param name="input">The items streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<VideoFrame> ReadVideoFramesAsync(IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                yield return item.ToVideoFrame();
            }
        }


        /// <summary>
        /// Creates the upscale pipeline.
        /// </summary>
        /// <param name="upscaleConfig">The upscale configuration.</param>
        /// <returns>UpscalePipeline.</returns>
        private UpscalePipeline CreateUpscalePipeline(UpscaleConfig upscaleConfig)
        {
            if (upscaleConfig == null)
                throw new Exception("Upscale pipeline requires an upscale configuration.");

            _executionProvider = upscaleConfig.DeviceType == DeviceType.CPU
                ? Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_ENABLE_ALL)
                : Provider.GetProvider(DeviceType.GPU, _pipelineOptions.DeviceId, GraphOptimizationLevel.ORT_ENABLE_ALL);

            var upscalerConfig = new UpscalerConfig
            {
                Channels = upscaleConfig.Channels,
                Normalization = upscaleConfig.Normalization,
                OutputNormalization = upscaleConfig.OutputNormalization,
                SampleSize = upscaleConfig.SampleSize,
                ScaleFactor = upscaleConfig.ScaleFactor,
                Path = upscaleConfig.ModelPath
            };
            upscalerConfig.SetProvider(_executionProvider);
            return UpscalePipeline.Create(upscalerConfig);
        }


        /// <summary>
        /// Upscales the image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The upscaled image.</returns>
        private async Task<ImageTensor> UpscaleImageAsync(UpscaleImageRunOptions options, CancellationToken cancellationToken)
        {
            if (_pipeline is not UpscalePipeline upscalePipeline)
                throw new Exception("Upscale requires an upscale pipeline to be loaded.");

            var inputImage = options.InputImages.FirstOrDefault() ?? throw new Exception("No input image to upscale.");
            var upscaleOptions = new UpscaleImageOptions
            {
                Image = inputImage,
                IsTileEnabled = options.IsTileEnabled,
                MaxTileSize = options.TileSize,
                TileOverlap = options.TileOverlap
            };
            return await Task.Run(() => upscalePipeline.RunAsync(upscaleOptions, _progressRelayRunCallback, cancellationToken), cancellationToken);
        }


        /// <summary>
        /// Upscales the video frames streamed from the client, returning each frame as it is upscaled.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="input">The frames streamed from the client.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async IAsyncEnumerable<PipelineStreamItem> UpscaleStreamAsync(UpscaleImageRunOptions options, IAsyncEnumerable<PipelineStreamItem> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_pipeline is not UpscalePipeline upscalePipeline)
                throw new Exception("Upscale requires an upscale pipeline to be loaded.");

            var upscaleOptions = new UpscaleStreamOptions
            {
                IsTileEnabled = options.IsTileEnabled,
                MaxTileSize = options.TileSize,
                TileOverlap = options.TileOverlap,
                Stream = ReadVideoFramesAsync(input, cancellationToken)
            };

            await foreach (var frame in upscalePipeline.RunAsync(upscaleOptions, _progressRelayRunCallback, cancellationToken))
            {
                yield return frame.ToStreamItem();
            }
        }


        /// <summary>
        /// Creates the extract pipeline.
        /// </summary>
        /// <param name="extractConfig">The extract configuration.</param>
        /// <returns>The extract pipeline for the extractor type.</returns>
        private IPipeline CreateExtractPipeline(ExtractConfig extractConfig)
        {
            if (extractConfig == null)
                throw new Exception("Extract pipeline requires an extract configuration.");

            _executionProvider = extractConfig.DeviceType == DeviceType.CPU
                ? Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_DISABLE_ALL)
                : Provider.GetProvider(DeviceType.GPU, _pipelineOptions.DeviceId, GraphOptimizationLevel.ORT_DISABLE_ALL);

            var extractorConfig = new ExtractorConfig
            {
                Channels = extractConfig.Channels,
                Normalization = extractConfig.Normalization,
                OutputChannels = extractConfig.OutputChannels,
                OutputNormalization = extractConfig.OutputNormalization,
                IsDynamicOutput = extractConfig.IsDynamicOutput,
                SampleSize = extractConfig.SampleSize,
                Path = extractConfig.ModelPath
            };
            extractorConfig.SetProvider(_executionProvider);
            return extractConfig.Type switch
            {
                ExtractType.Pose => PosePipeline.Create(extractorConfig),
                ExtractType.Background => BackgroundPipeline.Create(extractorConfig),
                _ => ExtractorPipeline.Create(extractorConfig)
            };
        }


        /// <summary>
        /// Extracts the image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The extracted image.</returns>
        private async Task<ImageTensor> ExtractImageAsync(ExtractImageRunOptions options, CancellationToken cancellationToken)
        {
            var inputImage = options.InputImages.FirstOrDefault() ?? throw new Exception("No input image to extract.");
            return _pipeline switch
            {
                ExtractorPipeline extractorPipeline => await Task.Run(() => extractorPipeline.RunAsync(new ExtractorImageOptions
                {
                    Image = inputImage,
                    IsInverted = options.IsInverted,
                    IsTileEnabled = options.IsTileEnabled,
                    MaxTileSize = options.TileSize,
                    TileOverlap = options.TileOverlap,
                    MergeInput = options.MergeInput
                }, _progressRelayRunCallback, cancellationToken), cancellationToken),
                BackgroundPipeline backgroundPipeline => await Task.Run(() => backgroundPipeline.RunAsync(new BackgroundImageOptions
                {
                    Image = inputImage,
                    Mode = (TensorStack.OnnxRuntime.Extract.BackgroundMode)options.Mode,
                    IsTransparentSupported = options.IsTransparentSupported
                }, _progressRelayRunCallback, cancellationToken), cancellationToken),
                PosePipeline posePipeline => await Task.Run(() => posePipeline.RunAsync(new PoseImageOptions
                {
                    Image = inputImage,
                    Detections = options.Detections,
                    BodyConfidence = options.BodyConfidence,
                    JointConfidence = options.JointConfidence,
                    ColorAlpha = options.ColorAlpha,
                    JointRadius = options.JointRadius,
                    BoneRadius = options.BoneRadius,
                    BoneThickness = options.BoneThickness,
                    IsTransparent = options.IsTransparent
                }, _progressRelayRunCallback, cancellationToken), cancellationToken),
                _ => throw new Exception("Extract requires an extract pipeline to be loaded.")
            };
        }


        private void UpdateProgress(RunProgress progress)
        {
            EnqueueProgress(new PipelineProgress
            {
                Key = "Generate",
                Subkey = "Step",
                Value = progress.Value,
                Maximum = progress.Maximum,
                Message = progress.Message,
                Elapsed = (float)progress.Elapsed.TotalMilliseconds
            });
        }

    }
}