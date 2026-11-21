using Amuse.App.Common;
using Amuse.App.Resources;
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
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Image;

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
        protected BackendClient(Settings settings, IToolService toolService, IMediaService mediaService, ILogger logger)
        {
            Logger = logger;
            Settings = settings;
            ToolService = toolService;
            MediaService = mediaService;
        }

        public PipelineModel Pipeline { get; protected set; }
        public GenerateDefaultOptions DefaultOptions { get; protected set; }
        public bool StopHostOnException { get; protected set; }
        public bool ResolveComponentFiles { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the client is connected to its host, false once the host closed unexpectedly.
        /// </summary>
        public bool IsConnected => PipelineClient is not null;
        protected ILogger Logger { get; }
        protected Settings Settings { get; }
        protected IToolService ToolService { get; }
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
                    DefaultOptions = Pipeline.GenerateModel?.DefaultOptions;
                    PipelineClient = await CreatePipelineClientAsync(CancellationTokenSource.Token);
                    if (Pipeline.GenerateModel is not null)
                        Pipeline.GenerateModel.Status = ModelStatusType.Installed;
                    if (Pipeline.IsUpscaleOnly)
                        Pipeline.UpscaleModel.Status = ModelStatusType.Installed;
                    if (Pipeline.IsExtractOnly)
                        Pipeline.ExtractModel.Status = ModelStatusType.Installed;
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
                // Release the channels and progress loop, not just the reference
                PipelineClient?.Dispose();
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
                        throw new Exception(AppErrors.GeneratedImageNotFound);

                    return await ImageInput.CreateAsync(imageFileName);
                }
                return imageTensors[0];
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
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
                foreach (var inputAudio in options.InputAudios)
                {
                    // A default of zero keeps the source sample rate or channels
                    generateOptions.InputAudios.Add(await inputAudio.GetAsync(DefaultOptions.SampleRate, DefaultOptions.Channels));
                }

                var videoSequences = await PipelineClient.GenerateVideoAsync(generateOptions);
                if (videoSequences.IsNullOrEmpty())
                {
                    if (!File.Exists(videoFileName))
                        throw new Exception(AppErrors.GeneratedVideoNotFound);

                    return new VideoInputStream(videoFileName);
                }

                await videoSequences[0].SaveAsync(videoFileName);
                return new VideoInputStream(videoFileName);
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
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
                        throw new Exception(AppErrors.GeneratedAudioNotFound);

                    return await AudioInputStream.CreateAsync(audioFileName);
                }

                var audioInput = new AudioInput(audioTensors[0]);
                await audioInput.SaveAsync(audioFileName);
                return await AudioInputStream.CreateAsync(audioFileName);
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
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

                if (DefaultOptions.IsToolCallsSupported && options.IsToolCallsEnabled)
                {
                    generateOptions.Tools = ToolService.GetTools(options);
                }

                var pipelineResult = await GenerateTextLoopAsync(generateOptions);
                foreach (var beamResult in pipelineResult)
                {
                    textResult.Results.Add(beamResult);
                }
                return textResult;
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
            }
        }


        /// <summary>
        /// Generate text with tool loop
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<IReadOnlyList<TextInput>> GenerateTextLoopAsync(Amuse.Common.GenerateTextOptions options)
        {
            var textResult = await PipelineClient.GenerateTextAsync(options);
            if (DefaultOptions.IsToolCallsSupported && ToolService.IsToolResponse(textResult))
            {
                var toolResponse = ToolService.ParseResponse(textResult);
                if (toolResponse == null || toolResponse.IsEmpty)
                    return textResult;

                options.Conversation.Add(new ConversationMessage(ConversationRole.Assistant, "", default, default, toolResponse.ToolCalls));
                foreach (var tool in toolResponse.Tools)
                {
                    var content = await tool.ExecuteAsync(Settings);
                    options.Conversation.Add(new ConversationMessage(ConversationRole.Tool, content, default, default, default));
                }
                textResult = await GenerateTextLoopAsync(options);
            }
            return textResult;
        }


        /// <summary>
        /// Upscale image
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="options">The upscale options.</param>
        /// <exception cref="Exception">Upscaled image result not found.</exception>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<ImageTensor> UpscaleImageAsync(ImageTensor inputImage, UpscaleInputOptions options)
        {
            try
            {
                var upscaleOptions = new UpscaleImageRunOptions
                {
                    IsTileEnabled = options?.IsTileEnabled ?? false,
                    TileSize = options?.TileSize ?? 0,
                    TileOverlap = options?.TileOverlap ?? 0,
                    InputImages = [inputImage]
                };

                var imageTensors = await PipelineClient.UpscaleImageAsync(upscaleOptions);
                if (imageTensors.IsNullOrEmpty())
                    throw new Exception(AppErrors.UpscaledImageNotFound);

                return imageTensors[0];
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
            }
        }


        /// <summary>
        /// Upscale video
        /// </summary>
        /// <param name="inputVideo">The input video.</param>
        /// <param name="options">The upscale options.</param>
        /// <exception cref="Exception">Upscaled video result not found.</exception>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<VideoSequence> UpscaleVideoAsync(VideoSequence inputVideo, UpscaleInputOptions options)
        {
            try
            {
                var upscaleOptions = new UpscaleVideoRunOptions
                {
                    Seed = options?.Seed > 0 ? options.Seed : Random.Shared.Next(1, int.MaxValue),
                    WindowSize = options?.WindowSize ?? 0,
                    TemporalChunkSize = options?.TemporalChunkSize ?? 0,
                    InputVideos = [inputVideo]
                };

                var videoSequences = await PipelineClient.UpscaleVideoAsync(upscaleOptions);
                if (videoSequences.IsNullOrEmpty())
                    throw new Exception(AppErrors.UpscaledVideoNotFound);

                return videoSequences[0];
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
            }
        }


        /// <summary>
        /// Extract image
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="options">The extract options.</param>
        /// <param name="isTransparentSupported">Whether a transparent result is supported, background extractors.</param>
        /// <exception cref="Exception">Extracted image result not found.</exception>
        /// <exception cref="Exception">Pipeline Closed Unexpectedly</exception>
        public async Task<ImageTensor> ExtractImageAsync(ImageTensor inputImage, ExtractInputOptions options, bool isTransparentSupported = true)
        {
            try
            {
                var extractOptions = new ExtractImageRunOptions
                {
                    IsTileEnabled = options?.IsTileEnabled ?? false,
                    TileSize = options?.TileSize ?? 0,
                    TileOverlap = options?.TileOverlap ?? 0,
                    IsInverted = options?.IsInverted ?? false,
                    MergeInput = options?.MergeInput ?? false,
                    Mode = options?.Mode ?? default,
                    IsTransparentSupported = isTransparentSupported,
                    Detections = options?.Detections ?? 0,
                    BodyConfidence = options?.BodyConfidence ?? 0,
                    JointConfidence = options?.JointConfidence ?? 0,
                    ColorAlpha = options?.ColorAlpha ?? 0,
                    JointRadius = options?.JointRadius ?? 0,
                    BoneRadius = options?.BoneRadius ?? 0,
                    BoneThickness = options?.BoneThickness ?? 0,
                    IsTransparent = options?.IsTransparent ?? false,
                    InputImages = [inputImage]
                };

                var imageTensors = await PipelineClient.ExtractImageAsync(extractOptions);
                if (imageTensors.IsNullOrEmpty())
                    throw new Exception(AppErrors.ExtractedImageNotFound);

                return imageTensors[0];
            }
            catch (IOException ex)
            {
                HandlePipelineClientError(ex);
                throw new Exception(AppErrors.PipelineClosedUnexpectedly);
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
            if (Pipeline.DiffusionModel != null)
                return GetPipelineLoadOptions(Pipeline.DiffusionModel);
            if (Pipeline.IsExtractOnly)
                return GetPipelineLoadOptions(Pipeline.ExtractModel);

            return GetPipelineLoadOptions(Pipeline.UpscaleModel);
        }


        /// <summary>
        /// Gets the load options for an extract-only pipeline.
        /// </summary>
        /// <param name="model">The extract model.</param>
        private PipelineLoadOptions GetPipelineLoadOptions(ExtractModel model)
        {
            var device = Pipeline.Device;
            return new PipelineLoadOptions
            {
                Pipeline = model.Pipeline,
                ProcessType = Pipeline.ProcessType,
                Device = device.DeviceCode,
                DeviceId = device.DeviceId,
                DeviceBusId = device.PCIBusId,
                DeviceVendor = device.Vendor,
                DeviceVendorIndex = device.VendorIndex,
                MemoryMode = GetMemoryMode(model.MemorySize),
                ExecutionOptions = GetExecutionOptions(),
                ExtractConfig = GetExtractConfig(model)
            };
        }


        /// <summary>
        /// Gets the extract configuration, resolving the checkpoint path.
        /// </summary>
        /// <param name="model">The extract model.</param>
        private ExtractConfig GetExtractConfig(ExtractModel model)
        {
            if (model is null)
                return null;

            return new ExtractConfig
            {
                Type = (ExtractType)model.Type,
                DeviceType = Pipeline.Device.Type,
                ModelPath = model.Checkpoint?.Resolve(Settings, Settings.DirectoryExtract, ResolveComponentFiles),
                Channels = model.Channels,
                SampleSize = model.SampleSize,
                Normalization = model.Normalization,
                OutputNormalization = model.OutputNormalization,
                OutputChannels = model.OutputChannels,
                IsDynamicOutput = model.IsDynamicOutput
            };
        }


        /// <summary>
        /// Gets the load options for an upscale-only pipeline.
        /// </summary>
        /// <param name="model">The upscale model.</param>
        private PipelineLoadOptions GetPipelineLoadOptions(UpscaleModel model)
        {
            var device = Pipeline.Device;
            return new PipelineLoadOptions
            {
                Pipeline = model.Pipeline,
                ProcessType = Pipeline.ProcessType,
                Device = device.DeviceCode,
                DeviceId = device.DeviceId,
                DeviceBusId = device.PCIBusId,
                DeviceVendor = device.Vendor,
                DeviceVendorIndex = device.VendorIndex,
                MemoryMode = GetMemoryMode(model.MemorySize),
                ExecutionOptions = GetExecutionOptions(),
                UpscaleConfig = GetUpscaleConfig(model)
            };
        }


        /// <summary>
        /// Gets the upscale configuration, resolving the checkpoint paths.
        /// </summary>
        /// <param name="model">The upscale model.</param>
        private UpscaleConfig GetUpscaleConfig(UpscaleModel model)
        {
            if (model is null)
                return null;

            var resolveFiles = ResolveComponentFiles;
            var modelPath = model.Checkpoint?.Resolve(Settings, Settings.DirectoryUpscale, resolveFiles);
            var vaePath = model.Vae?.Resolve(Settings, Settings.DirectoryUpscale, resolveFiles);
            return new UpscaleConfig
            {
                Pipeline = model.Pipeline,
                DeviceType = Pipeline.Device.Type,
                ModelPath = model.IsVideoUpscaler ? null : modelPath,
                DiffusionModelPath = model.IsVideoUpscaler ? modelPath : null,
                VaePath = vaePath,
                ScaleFactor = model.ScaleFactor,
                TileSize = model.DefaultOptions?.TileSize ?? 0,
                Channels = model.Channels,
                SampleSize = model.SampleSize,
                Normalization = model.Normalization,
                OutputNormalization = model.OutputNormalization
            };
        }


        private PipelineLoadOptions GetPipelineLoadOptions(DiffusionModel model)
        {
            var device = Pipeline.Device;
            return new PipelineLoadOptions
            {
                Variant = model.Variant,
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
                MemoryMode = GetMemoryMode(model.MemorySize),
                ControlNet = ControlNetConfig(),
                LoraAdapters = GetLoraAdapters(),
                CheckpointConfig = GetCheckpoint(model.Checkpoint, Settings.DirectoryDiffusion),
                ExecutionOptions = GetExecutionOptions()
            };
        }


        private PipelineLoadOptions GetPipelineLoadOptions(LanguageModel model)
        {
            var device = Pipeline.Device;
            return new PipelineLoadOptions
            {
                Variant = model.Variant,
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
                MemoryMode = GetMemoryMode(model.MemorySize),
                LoraAdapters = GetLoraAdapters(),
                CheckpointConfig = GetCheckpoint(model.Checkpoint, Settings.DirectoryLangaugeModel),
                ExecutionOptions = GetExecutionOptions()
            };
        }


        private CheckpointConfig GetCheckpoint(DiffusionCheckpointModel checkpoint, string modelDirectory)
        {
            var resolveFiles = ResolveComponentFiles;
            var checkpointConfig = new CheckpointConfig
            {
                Compute = checkpoint.Compute?.Resolve(Settings, modelDirectory, resolveFiles),
                Tokenizer = checkpoint.Tokenizer?.Resolve(Settings, modelDirectory, resolveFiles),
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
            var resolveFiles = ResolveComponentFiles;
            var checkpointConfig = new CheckpointConfig
            {
                TextEncoder = checkpoint.TextModel?.Resolve(Settings, modelDirectory, resolveFiles),
                TextEncoder2 = checkpoint.TextModel2?.Resolve(Settings, modelDirectory, resolveFiles),
            };
            return checkpointConfig;
        }


        /// <summary>
        /// Gets the backend memory mode, resolving Auto from the model memory size and device memory.
        /// </summary>
        /// <param name="memorySize">The model memory size in GB.</param>
        private MemoryModeType GetMemoryMode(float memorySize)
        {
            var memoryMode = Pipeline.MemoryMode;
            if (memoryMode == MemoryMode.Auto)
                memoryMode = Extensions.ResolveMemoryMode(memorySize, Pipeline.Device.MemoryGB);

            return memoryMode switch
            {
                MemoryMode.Balanced => MemoryModeType.Balanced,
                MemoryMode.Low => MemoryModeType.OffloadCPU,
                MemoryMode.Medium => MemoryModeType.OffloadModel,
                MemoryMode.High => MemoryModeType.Device,
                _ => MemoryModeType.Device,
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


        private ExecutionOptions GetExecutionOptions()
        {
            var options = Pipeline.ExecutionOptions;
            if (options is null)
                return null;

            return new ExecutionOptions
            {
                IsPrefetchEnabled = options.IsPrefetchEnabled,
                IsSegmentedComputeEnabled = options.IsSegmentedComputeEnabled,
                MemoryReserve = options.MemoryReserve,
                GpuLayers = options.GpuLayers,
                ContextSize = options.ContextSize,
                Threads = options.Threads,
                IsCacheOffloadEnabled = options.IsCacheOffloadEnabled,
                IsControlNetCpuEnabled = options.IsControlNetCpuEnabled,
                IsAutoEncoderCpuEnabled = options.IsAutoEncoderCpuEnabled,
                IsTextEncoderCpuEnabled = options.IsTextEncoderCpuEnabled,
                DiffusionConvDirect = options.DiffusionConvDirect,
                ControlNetConvDirect = options.ControlNetConvDirect,
                AutoEncoderConvDirect = options.AutoEncoderConvDirect,
                TextEncoderConvDirect = options.TextEncoderConvDirect,
                DiffusionFlashAttention = options.DiffusionFlashAttention,
                ControlNetFlashAttention = options.ControlNetFlashAttention,
                AutoEncoderFlashAttention = options.AutoEncoderFlashAttention,
                TextEncoderFlashAttention = options.TextEncoderFlashAttention,
                IsSageAttentionEnabled = options.IsSageAttentionEnabled,
                IsDeviceQuantizationEnabled = options.IsDeviceQuantizationEnabled,
                IsOptimizeDeviceEnabled = options.IsOptimizeDeviceEnabled,
                IsOptimizeChannelsEnabled = options.IsOptimizeChannelsEnabled,
                WeightType = options.WeightType,
            };
        }


        private static List<ConversationMessage> CreateConversation(ObservableCollection<ConversationModel> conversation)
        {
            if (conversation.IsNullOrEmpty())
                return default;

            return [.. conversation.Select(x => new ConversationMessage(x.Role, x.Content, x.ImageIndex.GetIndexValues(), x.AudioIndex.GetIndexValues(), default))];
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
