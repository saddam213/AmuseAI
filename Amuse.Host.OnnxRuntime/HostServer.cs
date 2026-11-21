using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.OnnxRuntime;
using TensorStack.OnnxRuntime.Extract;
using TensorStack.OnnxRuntime.LLM.Common;
using TensorStack.OnnxRuntime.LLM.Pipelines.Supertonic;
using TensorStack.OnnxRuntime.LLM.Pipelines.Whisper;
using TensorStack.OnnxRuntime.Upscale;
using PipelineProgress = Amuse.Common.PipelineProgress;


namespace Amuse.Host.OnnxRuntime
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<RunProgress> _progressRelayRunCallback;
        private readonly IProgress<GenerateProgress> _progressRelayGenerateCallback;

        private IPipeline _pipeline;
        private PipelineLoadOptions _pipelineOptions;
        private ExecutionProvider _executionProvider;
        private ExecutionProvider _executionProviderCPU;

        public HostServer(ServerConfig channelConfig, ILogger logger)
            : base(channelConfig, logger)
        {
            _progressRelayRunCallback = new SyncProgress<RunProgress>(UpdateProgress);
            _progressRelayGenerateCallback = new SyncProgress<GenerateProgress>(UpdateProgress);
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

                _executionProvider = Provider.GetProvider(DeviceType.GPU, _pipelineOptions.DeviceId, GraphOptimizationLevel.ORT_ENABLE_ALL);
                _executionProviderCPU = Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_ENABLE_ALL); // TODO: DirectML not working with decoder

                var onnxModelPath = _pipelineOptions.CheckpointConfig.Compute;

                _pipeline = _pipelineOptions.Pipeline switch
                {
                    PipelineType.SupertonicPipeline => SupertonicPipeline.Create(onnxModelPath, _executionProvider),
                    PipelineType.WhisperPipeline => WhisperPipeline.Create(_executionProvider, _executionProviderCPU, onnxModelPath, Enum.Parse<WhisperType>(_pipelineOptions.ModelType, true)),
                    _ => throw new NotImplementedException()
                };
                await _pipeline.LoadAsync(cancellationToken);
                await SendResponse(cancellationToken);

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
                else if (_pipelineOptions.ProcessType == Common.ProcessType.AudioToText)
                {
                    var resultTensor = await GenerateTextAsync(request.RunOptions.TextOptions, cancellationToken);
                    await SendTensorResponse(cancellationToken, resultTensor);
                }
                else if (_pipelineOptions.ProcessType == Common.ProcessType.TextToAudio)
                {
                    var resultTensor = await GenerateAudioAsync(request.RunOptions.AudioOptions, cancellationToken);
                    await SendTensorResponse(cancellationToken, resultTensor);
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


        private async Task<AudioTensor> GenerateAudioAsync(Common.GenerateAudioOptions options, CancellationToken cancellationToken)
        {
            var supertonicPipeline = _pipeline as IPipeline<AudioTensor, SupertonicOptions, RunProgress>;
            var pipelineOptions = new SupertonicOptions
            {
                TextInput = options.Prompt,
                VoiceStyle = options.Task,
                Steps = options.Steps,
                Speed = options.Speed,
                SilenceDuration = options.SilenceDuration,
                Seed = options.Seed,
                Language = options.Language.GetShortName(),
            };
            return await supertonicPipeline.RunAsync(pipelineOptions, _progressRelayRunCallback, cancellationToken);
        }


        public async Task<TextInput[]> GenerateTextAsync(Common.GenerateTextOptions options, CancellationToken cancellationToken)
        {
            var pipelineOptions = new WhisperOptions
            {
                Seed = options.Seed,
                Beams = options.Beams,
                TopK = options.TopK,
                TopP = options.TopP,
                Temperature = options.Temperature,
                MaxLength = options.MaxLength,
                MinLength = options.MinLength,
                NoRepeatNgramSize = options.NoRepeatNgramSize,
                LengthPenalty = options.LengthPenalty,
                EarlyStopping = Enum.Parse<EarlyStopping>(options.EarlyStopping, true),
                Language = options.GetLanguageType(),
                Task = Enum.Parse<TaskType>(options.Task),
                ChunkSize = options.ChunkSize,
                AudioInput = options.InputAudios[0]
            };

            var pipelineResult = await Task.Run(async () =>
            {
                if (options.Beams == 0)
                {
                    // Greedy Search
                    var greedyPipeline = _pipeline as IPipeline<GenerateResult, WhisperOptions, GenerateProgress>;
                    return [await greedyPipeline.RunAsync(pipelineOptions, _progressRelayGenerateCallback, cancellationToken)];
                }

                // Beam Search
                var beamSearchPipeline = _pipeline as IPipeline<GenerateResult[], WhisperSearchOptions, GenerateProgress>;
                return await beamSearchPipeline.RunAsync(new WhisperSearchOptions(pipelineOptions), _progressRelayGenerateCallback, cancellationToken);
            });

            var results = new TextInput[pipelineResult.Length];
            for (int i = 0; i < pipelineResult.Length; i++)
            {
                var beamResult = pipelineResult[i];
                results[i] = new TextInput
                {
                    Beam = beamResult.Beam,
                    PenaltyScore = beamResult.PenaltyScore,
                    Score = beamResult.Score,
                    Text = beamResult.Result
                };
            }
            return results;
        }


        private void UpdateProgress(GenerateProgress progress)
        {
            EnqueueProgress(new PipelineProgress
            {
                Key = "Generate",
                Subkey = $"{progress.IsReset}",
                Value = progress.Value,
                Maximum = progress.Maximum,
                Message = progress.Result
            });
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