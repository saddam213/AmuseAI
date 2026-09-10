using Amuse.App.Common;
using Amuse.App.Services;
using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Media;
using TensorStack.Media.Image;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;

namespace Amuse.App.Runtime
{
    public abstract class BackendClient : ServiceBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BackendClient"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        protected BackendClient(Settings settings, IMediaService mediaService, ILogger logger)
        {
            Logger = logger;
            Settings = settings;
            MediaService = mediaService;
        }

        public PipelineModel Pipeline { get; protected set; }
        public GenerateDefaultOptions DefaultOptions { get; protected set; }
        public bool StopHostOnException { get; protected set; }
        public bool ResolveComponentFiles { get; protected set; }
        protected ILogger Logger { get; }
        protected Settings Settings { get; }
        protected IMediaService MediaService { get; }
        protected PipelineClient PipelineClient { get; set; }
        protected IProgress<PipelineProgress> ProgressCallback { get; set; }
        protected CancellationTokenSource CancellationTokenSource { get; set; }
        protected abstract Task<PipelineClient> CreatePipelineClientAsync(CancellationToken cancellationToken = default);


        /// <summary>
        /// Load the pipeline
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="progressCallback">The progress callback.</param>
        public async Task LoadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback)
        {
            try
            {
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    await UnloadPipelineClientAsync();

                    Pipeline = pipeline;
                    ProgressCallback = progressCallback;
                    DefaultOptions = Pipeline.GenerateModel.DefaultOptions;
                    PipelineClient = await CreatePipelineClientAsync(CancellationTokenSource.Token);
                    Pipeline.GenerateModel.Status = ModelStatusType.Installed;
                    Settings.ScanModels();
                }
            }
            catch (OperationCanceledException)
            {
                PipelineClient?.Dispose();
                PipelineClient = null;
                DefaultOptions = null;
                Pipeline = null;
                throw;
            }
            finally
            {
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public async Task ReloadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback)
        {
            try
            {
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    Pipeline = pipeline;
                    ProgressCallback = progressCallback;
                    var reloadOptions = new PipelineReloadOptions
                    {
                        ControlNet = ControlNetConfig(),
                        LoraAdapters = GetLoraAdapters(),
                        ProcessType = pipeline.ProcessType,
                    };

                    await PipelineClient.ReloadAsync(reloadOptions, CancellationTokenSource.Token);
                    Settings.ScanModels();
                }
            }
            catch (OperationCanceledException)
            {
                PipelineClient?.Dispose();
                PipelineClient = null;
                DefaultOptions = null;
                Pipeline = null;
                throw;
            }
            finally
            {
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Updates the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <returns>Task.</returns>
        public Task UpdateAsync(PipelineModel pipeline)
        {
            Pipeline = pipeline;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Cancel the running task (Load or Execute)
        /// </summary>
        public async Task CancelAsync()
        {
            try
            {
                if (PipelineClient is not null)
                    await PipelineClient.CancelAsync();
            }
            catch (Exception) { }
            finally
            {
                await CancellationTokenSource.SafeCancelAsync();
            }
        }


        /// <summary>
        /// Stop/Kill server
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                await PipelineClient.KillServerAsync();
            }
            catch (Exception) { }
            finally
            {
                PipelineClient = null;
            }
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            await CancelAsync();
            await UnloadPipelineClientAsync();
            Pipeline = null;
            DefaultOptions = null;
        }


        /// <summary>
        /// Generate image
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<ImageTensor> GenerateImageAsync(GenerateInputOptions options)
        {
            try
            {
                var imageFileName = MediaService.GetTempFile(MediaType.Image);
                options.Seed = options.Seed > 0 ? options.Seed : Random.Shared.Next();
                options.NegativePrompt = options.GuidanceScale > 1f && string.IsNullOrEmpty(options.NegativePrompt) ? " " : options.NegativePrompt;
                var generateOptions = GenerateImageOptions(options, imageFileName);
                var imageTensors = await PipelineClient.GenerateImageAsync(generateOptions);
                if (imageTensors.IsNullOrEmpty())
                {
                    if (!File.Exists(imageFileName))
                        throw new Exception("Generated image result not found.");

                    return await ImageInput.CreateAsync(imageFileName);
                }
                return imageTensors[0];
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception("Pipeline Closed Unexpectedly");
            }
        }


        /// <summary>
        /// Generate video
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="Exception">Generated video result not found.</exception>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<VideoInputStream> GenerateVideoAsync(GenerateInputOptions options)
        {
            try
            {
                var videoFileName = MediaService.GetTempFile(MediaType.Video);
                options.Seed = options.Seed > 0 ? options.Seed : Random.Shared.Next();
                options.NegativePrompt = options.GuidanceScale > 1f && string.IsNullOrEmpty(options.NegativePrompt) ? " " : options.NegativePrompt;
                var generateOptions = GenerateVideoOptions(options, videoFileName);
                var videoSequences = await PipelineClient.GenerateVideoAsync(generateOptions);
                if (videoSequences.IsNullOrEmpty())
                {
                    if (!File.Exists(videoFileName))
                        throw new Exception("Generated video result not found.");

                    return new VideoInputStream(videoFileName);
                }

                await videoSequences[0].SaveAsync(videoFileName);
                return new VideoInputStream(videoFileName);
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception("Pipeline Closed Unexpectedly");
            }
        }


        /// <summary>
        /// Generate audio
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="Exception">Generated video result not found.</exception>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<AudioInputStream> GenerateAudioAsync(GenerateInputOptions options)
        {
            try
            {
                var audioFileName = MediaService.GetTempFile(MediaType.Audio);
                options.Seed = options.Seed > 0 ? options.Seed : Random.Shared.Next();
                var generateOptions = GenerateAudioOptions(options, audioFileName);
                foreach (var inputAudios in options.InputAudios)
                {
                    generateOptions.InputAudios.Add(await inputAudios.GetAsync(DefaultOptions.SampleRate, DefaultOptions.Channels));
                }

                var audioTensors = await PipelineClient.GenerateAudioAsync(generateOptions);
                if (audioTensors.IsNullOrEmpty())
                {
                    if (!File.Exists(audioFileName))
                        throw new Exception("Generated audio result not found.");

                    return await AudioInputStream.CreateAsync(audioFileName);
                }

                var audioInput = new AudioInput(audioTensors[0]);
                await audioInput.SaveAsync(audioFileName);
                return await AudioInputStream.CreateAsync(audioFileName);
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception("Pipeline Closed Unexpectedly");
            }
        }


        /// <summary>
        /// Generate text
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>A Task&lt;TextResult&gt; representing the asynchronous operation.</returns>
        /// <exception cref="System.Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<TextResult> GenerateTextAsync(GenerateInputOptions options)
        {
            try
            {
                var textResult = new TextResult();
                var textFileName = MediaService.GetTempFile(MediaType.Text);
                options.Seed = options.Seed > 0 ? options.Seed : Random.Shared.Next();
                var generateOptions = GenerateTextOptions(options, textFileName);
                foreach (var inputAudios in options.InputAudios)
                {
                    generateOptions.InputAudios.Add(await inputAudios.GetAsync(DefaultOptions.SampleRate, DefaultOptions.Channels));
                }

                var pipelineResult = await PipelineClient.GenerateTextAsync(generateOptions);
                foreach (var beamResult in pipelineResult)
                {
                    textResult.Results.Add(beamResult);
                }
                return textResult;
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception("Pipeline Closed Unexpectedly");
            }
        }


        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            PipelineClient?.Dispose();
            PipelineClient = null;
            CancellationTokenSource = null;
            Pipeline = null;
            DefaultOptions = null;
            ProgressCallback = null;
        }


        /// <summary>
        /// Create pipeline client
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task<PipelineClient> CreatePipelineClientAsync(ClientConfig config, PipelineCreateOptions createOptions, CancellationToken cancellationToken)
        {
            var progressCallback = new Progress<PipelineProgress>(progress => ProgressCallback?.Report(progress));
            var pipelineClient = new PipelineClient(config, progressCallback, Logger);

            try
            {
                var loadOptions = GetPipelineLoadOptions();
                await pipelineClient.CreateAsync(createOptions, cancellationToken);
                await pipelineClient.LoadAsync(loadOptions, cancellationToken);
                return pipelineClient;
            }
            catch (Exception)
            {
                pipelineClient?.Dispose();
                throw;
            }
        }


        /// <summary>
        /// Unload pipeline client.
        /// </summary>
        protected async Task UnloadPipelineClientAsync()
        {
            try
            {
                if (PipelineClient != null)
                    await PipelineClient.UnloadAsync();
            }
            catch (Exception)
            {
            }
            finally
            {
                PipelineClient?.Dispose();
                PipelineClient = null;
            }
        }


        /// <summary>
        /// Handles the pipeline client error.
        /// </summary>
        /// <param name="exception">The exception.</param>
        protected void HandlePipelineClientError(Exception exception)
        {
            try
            {
                PipelineClient?.Dispose();
            }
            catch (Exception) { }
            finally
            {
                PipelineClient = null;
                Pipeline = null;
                DefaultOptions = null;
            }
        }


        protected GenerateImageOptions GenerateImageOptions(GenerateInputOptions options, string tempFileName)
        {
            return new GenerateImageOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Prompt2 = options.Prompt2,
                NegativePrompt = options.NegativePrompt,
                GuidanceScale = options.GuidanceScale,
                GuidanceScale2 = options.GuidanceScale2,
                Steps = options.Steps,
                Steps2 = options.Steps2,
                Width = options.Width,
                Height = options.Height,
                Strength = options.Strength,
                ControlNetScale = options.ControlNetStrength,
                TempFileName = tempFileName,
                EnableVaeSlicing = options.IsVaeSlicingEnabled,
                EnableVaeTiling = options.IsVaeTilingEnabled,
                Language = options.Language,
                Instruction = options.Instruction,
                Task = options.Task,
                MaxLength = DefaultOptions.MaxLength,
                MaxLength2 = DefaultOptions.MaxLength2,
                LatentUpscale = options.LatentUpscale,
                LatentUpscaleSteps = options.LatentUpscaleSteps,
                LatentUpscaleStrength = options.LatentUpscaleStrength,
                LatentUpscaleTileSize = options.LatentUpscaleTileSize,
                SchedulerOptions = options.SchedulerOptions?.ToClientOptions(),
                LoraOptions = LoraOptions(options),
                InputImages = options.InputImages,
                InputControlImages = options.InputControlImages
            };
        }


        protected GenerateVideoOptions GenerateVideoOptions(GenerateInputOptions options, string tempFileName)
        {
            return new GenerateVideoOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Prompt2 = options.Prompt2,
                NegativePrompt = options.NegativePrompt,
                GuidanceScale = options.GuidanceScale,
                GuidanceScale2 = options.GuidanceScale2,
                Steps = options.Steps,
                Steps2 = options.Steps2,
                Width = options.Width,
                Height = options.Height,
                Frames = options.Frames,
                FrameRate = options.FrameRate,
                Strength = options.Strength,
                ControlNetScale = options.ControlNetStrength,
                TempFileName = tempFileName,
                FrameChunk = options.FrameChunk,
                FrameChunkOverlap = options.FrameChunkOverlap,
                NoiseCondition = options.NoiseCondition,
                EnableVaeSlicing = options.IsVaeSlicingEnabled,
                EnableVaeTiling = options.IsVaeTilingEnabled,
                Duration = options.Duration,
                Language = options.Language,
                Instruction = options.Instruction,
                Task = options.Task,
                MaxLength = DefaultOptions.MaxLength,
                MaxLength2 = DefaultOptions.MaxLength2,
                SampleRate = DefaultOptions.SampleRate,
                LatentUpscale = options.LatentUpscale,
                LatentUpscaleSteps = options.LatentUpscaleSteps,
                LatentUpscaleStrength = options.LatentUpscaleStrength,
                LatentUpscaleTileSize = options.LatentUpscaleTileSize,
                SchedulerOptions = options.SchedulerOptions?.ToClientOptions(),
                LoraOptions = LoraOptions(options),
                InputImages = options.InputImages,
                InputControlImages = options.InputControlImages,
                InputVideos = options.InputVideos,
            };
        }


        protected GenerateAudioOptions GenerateAudioOptions(GenerateInputOptions options, string tempFileName)
        {
            return new GenerateAudioOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Prompt2 = options.Prompt2,
                NegativePrompt = options.NegativePrompt,
                GuidanceScale = options.GuidanceScale,
                GuidanceScale2 = options.GuidanceScale2,
                Steps = options.Steps,
                Steps2 = options.Steps2,
                Strength = options.Strength,
                TempFileName = tempFileName,
                EnableVaeSlicing = options.IsVaeSlicingEnabled,
                EnableVaeTiling = options.IsVaeTilingEnabled,
                Duration = options.Duration,
                Language = options.Language,
                Instruction = options.Instruction,
                MaxLength = DefaultOptions.MaxLength,
                MaxLength2 = DefaultOptions.MaxLength2,
                Bpm = options.Bpm,
                Keyscale = options.Keyscale,
                Task = options.Task,
                TrackName = options.TrackName,
                TimeSignature = options.TimeSignature,
                Speed = options.Speed,
                SilenceDuration = options.SilenceDuration,
                SampleRate = DefaultOptions.SampleRate,
                SchedulerOptions = options.SchedulerOptions?.ToClientOptions(),
                LoraOptions = LoraOptions(options),
                InputVideos = options.InputVideos,
            };
        }


        protected GenerateTextOptions GenerateTextOptions(GenerateInputOptions options, string tempFileName)
        {
            return new GenerateTextOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Conversation = CreateConversation(options.Conversation),
                TempFileName = tempFileName,
                Language = options.Language,
                MinLength = options.MinLength,
                MaxLength = options.MaxLength,
                Beams = options.Beams,
                NoRepeatNgramSize = options.NoRepeatNgramSize,
                LengthPenalty = options.LengthPenalty,
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                TopH = options.TopH,
                TypicalP = options.TypicalP,
                RepetitionPenalty = options.RepetitionPenalty,
                IsSamplingEnabled = options.IsSamplingEnabled,
                ChunkSize = options.ChunkSize,
                EarlyStopping = options.EarlyStopping.ToString(),
                Instruction = options.Instruction,
                Task = options.Task,
                IsThinkingEnabled = options.IsThinkingEnabled,
                InputImages = options.InputImages,
                SampleRate = DefaultOptions.SampleRate,
                CacheType = options.CacheType,
                InputVideos = options.InputVideos,
            };
        }


        protected PipelineLoadOptions GetPipelineLoadOptions()
        {
            if (Pipeline.LanguageModel != null)
                return GetPipelineLoadOptions(Pipeline.LanguageModel);

            return GetPipelineLoadOptions(Pipeline.DiffusionModel);
        }


        private PipelineLoadOptions GetPipelineLoadOptions(DiffusionModel model)
        {
            var device = Pipeline.Device;
            var isFlashAttentionEnabled = model.DefaultOptions.IsFlashAttentionEnabled && device.IsFlashAttentionEnabled;
            return new PipelineLoadOptions
            {
                Variant = model.Variant,
                ModelPath = Path.GetFullPath(Settings.DirectoryDiffusion),
                LoraAdapterPath = Path.GetFullPath(Settings.DirectoryLoraAdapter),
                Template = model.Template,
                Pipeline = model.Pipeline,
                ModelType = model.ModelType,
                ProcessType = Pipeline.ProcessType,
                Device = device.DeviceCode,
                DeviceId = device.DeviceId,
                DeviceBusId = device.PCIBusId,
                DeviceVendor = device.Vendor,
                DeviceVendorIndex = device.VendorIndex,
                DataType = model.BaseType,
                IsOptimizeDeviceEnabled = Settings.IsOptimizeDeviceEnabled,
                IsOptimizeChannelsEnabled = Settings.IsOptimizeChannelsEnabled,
                IsDeviceQuantizationEnabled = Settings.IsDeviceQuantizationEnabled,
                IsFlashAttentionEnabled = isFlashAttentionEnabled,
                MemoryMode = GetMemoryMode(model.MemoryProfile),
                QuantType = GetQuantizationType(),
                ControlNet = ControlNetConfig(),
                LoraAdapters = GetLoraAdapters(),
                CheckpointConfig = GetCheckpoint(model.Checkpoint, Settings.DirectoryDiffusion)
            };
        }


        private PipelineLoadOptions GetPipelineLoadOptions(LanguageModel model)
        {
            var device = Pipeline.Device;
            var isFlashAttentionEnabled = model.DefaultOptions.IsFlashAttentionEnabled && device.IsFlashAttentionEnabled;
            return new PipelineLoadOptions
            {
                Variant = model.Variant,
                ModelPath = Path.GetFullPath(Settings.DirectoryLangaugeModel),
                LoraAdapterPath = Path.GetFullPath(Settings.DirectoryLoraAdapter),
                Template = model.Template,
                Pipeline = model.Pipeline,
                ModelType = model.ModelType,
                ProcessType = Pipeline.ProcessType,
                Device = device.DeviceCode,
                DeviceId = device.DeviceId,
                DeviceBusId = device.PCIBusId,
                DeviceVendor = device.Vendor,
                DataType = model.BaseType,
                IsOptimizeDeviceEnabled = Settings.IsOptimizeDeviceEnabled,
                IsOptimizeChannelsEnabled = Settings.IsOptimizeChannelsEnabled,
                IsDeviceQuantizationEnabled = Settings.IsDeviceQuantizationEnabled,
                IsFlashAttentionEnabled = isFlashAttentionEnabled,
                MemoryMode = MemoryModeType.Device,
                QuantType = GetQuantizationType(),
                LoraAdapters = GetLoraAdapters(),
                CheckpointConfig = GetCheckpoint(model.Checkpoint, Settings.DirectoryLangaugeModel)
            };
        }


        private CheckpointConfig GetCheckpoint(DiffusionCheckpointModel checkpoint, string modelDirectory)
        {
            var resolveFiles = ResolveComponentFiles;
            var checkpointConfig = new CheckpointConfig
            {
                Compute = checkpoint.Compute?.Resolve(Settings, modelDirectory, resolveFiles),
                TextEncoder = checkpoint.TextEncoder?.Resolve(Settings, modelDirectory, resolveFiles),
                TextEncoder2 = checkpoint.TextEncoder2?.Resolve(Settings, modelDirectory, resolveFiles),
                TextEncoder3 = checkpoint.TextEncoder3?.Resolve(Settings, modelDirectory, resolveFiles),
                Unet = checkpoint.Unet?.Resolve(Settings, modelDirectory, resolveFiles),
                Transformer = checkpoint.Transformer?.Resolve(Settings, modelDirectory, resolveFiles),
                Transformer2 = checkpoint.Transformer2?.Resolve(Settings, modelDirectory, resolveFiles),
                Vae = checkpoint.Vae?.Resolve(Settings, modelDirectory, resolveFiles),
                AudioVae = checkpoint.AudioVae?.Resolve(Settings, modelDirectory, resolveFiles),
                Vocoder = checkpoint.Vocoder?.Resolve(Settings, modelDirectory, resolveFiles),
                Connectors = checkpoint.Connectors?.Resolve(Settings, modelDirectory, resolveFiles),
                LatentUpsampler = checkpoint.LatentUpsampler?.Resolve(Settings, modelDirectory, resolveFiles),
                LatentUpsamplerTemporal = checkpoint.LatentUpsamplerTemporal?.Resolve(Settings, modelDirectory, resolveFiles),
                ConditionEncoder = checkpoint.ConditionEncoder?.Resolve(Settings, modelDirectory, resolveFiles),
                AudioTokenizer = checkpoint.AudioTokenizer?.Resolve(Settings, modelDirectory, resolveFiles),
                AudioDetokenizer = checkpoint.AudioDetokenizer?.Resolve(Settings, modelDirectory, resolveFiles),
            };
            return checkpointConfig;
        }


        private CheckpointConfig GetCheckpoint(LanguageCheckpointModel checkpoint, string modelDirectory)
        {
            var checkpointConfig = new CheckpointConfig
            {
                TextEncoder = checkpoint.TextModel?.Resolve(Settings, modelDirectory),
                TextEncoder2 = checkpoint.TextModel2?.Resolve(Settings, modelDirectory),
            };
            return checkpointConfig;
        }


        private MemoryModeType GetMemoryMode(MemoryProfile[] memoryProfiles)
        {
            var memoryMode = Pipeline.MemoryMode;
            if (memoryMode == MemoryMode.Auto)
            {
                var memoryProfile = memoryProfiles.FirstOrDefault(x => x.QualityMode == Pipeline.QualityMode);
                if (memoryProfile != null)
                {
                    var deviceMemory = Pipeline.Device.MemoryGB;
                    var modeIndex = memoryProfile.GetIndex(deviceMemory);
                    memoryMode = Enum.GetValues<MemoryMode>()[modeIndex + 2];
                }
            }

            return memoryMode switch
            {
                MemoryMode.Balanced => MemoryModeType.Balanced,
                MemoryMode.Low => MemoryModeType.OffloadCPU,
                MemoryMode.Medium => MemoryModeType.OffloadModel,
                MemoryMode.High => MemoryModeType.Device,
                _ => MemoryModeType.OffloadCPU,
            };
        }


        private QuantizationType GetQuantizationType()
        {
            return Pipeline.QualityMode switch
            {
                QualityMode.Draft => QuantizationType.Q4Bit,
                QualityMode.Standard => QuantizationType.Q8Bit,
                QualityMode.Production => QuantizationType.Q16Bit,
                _ => QuantizationType.Q8Bit,
            };
        }


        private List<LoraConfig> GetLoraAdapters()
        {
            var loraAdapters = Pipeline.LoraAdapterModel;
            if (loraAdapters.IsNullOrEmpty())
                return default;

            var loraConfigs = new List<LoraConfig>();
            var modelDirectory = Settings.DirectoryLoraAdapter;
            foreach (var loraAdapter in loraAdapters)
            {
                var resolvedCheckpoint = loraAdapter.Checkpoint?.Resolve(Settings, modelDirectory);
                var loraPath = Path.GetDirectoryName(resolvedCheckpoint);
                var loraWeights = Path.GetFileName(resolvedCheckpoint);
                loraConfigs.Add(new LoraConfig
                {
                    Path = loraPath,
                    Weights = loraWeights,
                    Name = loraAdapter.Key
                });
            }
            return loraConfigs;
        }


        private ControlNetConfig ControlNetConfig()
        {
            var model = Pipeline.ControlNetModel;
            if (model is null)
                return null;

            var resolvedCheckpoint = model.Checkpoint.Resolve(Settings, Settings.DirectoryControlNet, ResolveComponentFiles);
            return new ControlNetConfig
            {
                Name = model.Name,
                Path = resolvedCheckpoint,
                Invert = model.Invert,
                LayerCount = model.LayerCount,
                DisableProjections = model.DisableProjections
            };
        }


        private static ConversationMessage[] CreateConversation(ObservableCollection<ConversationModel> conversation)
        {
            if (conversation.IsNullOrEmpty())
                return default;

            return [.. conversation.Select(x => new ConversationMessage(x.Role, x.Content, x.ImageIndex.GetIndexValues(), x.AudioIndex.GetIndexValues()))];
        }


        private static List<LoraOptions> LoraOptions(GenerateInputOptions options)
        {
            if (options.LoraOptions.IsNullOrEmpty())
                return default;

            return [.. options.LoraOptions.Select(x => new LoraOptions
            {
                Name = x.Key,
                Strength = x.Strength
            })];
        }
    }
}
