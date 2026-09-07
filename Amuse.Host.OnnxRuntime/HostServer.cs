using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.OnnxRuntime;
using TensorStack.OnnxRuntime.LLM.Common;
using TensorStack.OnnxRuntime.LLM.Pipelines.Supertonic;
using TensorStack.OnnxRuntime.LLM.Pipelines.Whisper;


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
            _progressRelayRunCallback = new Progress<RunProgress>(async (p) => await UpdateProgress(p));
            _progressRelayGenerateCallback = new Progress<GenerateProgress>(async (p) => await UpdateProgress(p));
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
                _executionProvider = Provider.GetProvider(DeviceType.GPU, _pipelineOptions.DeviceId, GraphOptimizationLevel.ORT_ENABLE_ALL);
                _executionProviderCPU = Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_ENABLE_ALL); // TODO: DirectML not working with decoder

                Enum.TryParse<WhisperType>(_pipelineOptions.ModelType, true, out var WhisperType);

                var onnxModelPath = _pipelineOptions.CheckpointConfig.Compute;

                _pipeline = _pipelineOptions.Pipeline switch
                {
                    PipelineType.SupertonicPipeline => SupertonicPipeline.Create(onnxModelPath, _executionProvider),
                    PipelineType.WhisperPipeline => WhisperPipeline.Create(_executionProvider, _executionProviderCPU, onnxModelPath, WhisperType),
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
                if (_pipelineOptions.ProcessType == Common.ProcessType.AudioToText)
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


        private async Task UpdateProgress(GenerateProgress progress)
        {
            await QueueProgress(new PipelineProgress
            {
                Key = "Generate",
                Subkey = $"{progress.IsReset}",
                Value = progress.Value,
                Maximum = progress.Maximum,
                Message = progress.Result
            });
        }

        private async Task UpdateProgress(RunProgress progress)
        {
            await QueueProgress(new PipelineProgress
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