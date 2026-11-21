using Amuse.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using Pipeline = TensorStack.StableDiffusionCpp;
using PipelineCommon = TensorStack.StableDiffusionCpp.Common;

namespace Amuse.Host.StableDiffusionCpp
{
    internal static class Extensions
    {
        /// <summary>
        /// Creates the context options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.ContextOptions.</returns>
        /// <exception cref="System.NotImplementedException">Pipeline not supported</exception>
        internal static PipelineCommon.StableDiffusionOptions CreateContextOptions(this PipelineCreateOptions options, PipelineLoadOptions pipelineOptions)
        {
            var backendType = GetBackendType(options);
            var backendDevice = GetBackend(pipelineOptions, backendType);
            var paramsBackend = GetParamsBackend(pipelineOptions);
            var convDirectMode = GetConvDirect(pipelineOptions);
            var flashAttnMode = GetFlashAttention(pipelineOptions);
            var stableDiffusionOptions = new PipelineCommon.StableDiffusionOptions
            {
                Backend = backendDevice,
                ParamsBackend = paramsBackend,
                RngType = Pipeline.RngType.CPU,
                SamplerRngType = Pipeline.RngType.CPU,

                // Memory
                MaxVram = $"-{pipelineOptions.ExecutionOptions.MemoryReserve}",
                DataType = pipelineOptions.ExecutionOptions.WeightType.ToNativeDataType(),
                EagerLoad = false,
                IsPrefetchEnabled = pipelineOptions.ExecutionOptions.IsPrefetchEnabled,
                IsSegmentedComputeEnabled = pipelineOptions.ExecutionOptions.IsSegmentedComputeEnabled,
                AutoFit = pipelineOptions.MemoryMode == MemoryModeType.Balanced,

                // Misc
                ForceSdxlVaeConvScale = true,
                LoraApplyMode = Pipeline.LoraApplyType.AtRuntime,
                PreviewType = Pipeline.PreviewType.Projection,
                ConditioningCacheSize = 1,
                FlashAttnMode = flashAttnMode,
                ConvDirectMode = convDirectMode,
                SageAttn = backendType == Pipeline.SDBackendType.CUDA && pipelineOptions.ExecutionOptions.IsSageAttentionEnabled
            };


            if (pipelineOptions.Pipeline == PipelineType.StableDiffusionXLPipeline)
            {
                return stableDiffusionOptions with
                {
                    ModelPath = pipelineOptions.CheckpointConfig.FullCheckpoint,
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    ClipLPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    ClipGPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Unet,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.StableDiffusion3Pipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    ClipLPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    ClipGPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    T5xxlPath = pipelineOptions.CheckpointConfig.TextEncoder3,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.FluxPipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    ClipLPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    T5xxlPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.ChromaPipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    T5xxlPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.IdeogramPipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    UncondDiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer2,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.LTX20Pipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    AudioVaePath = pipelineOptions.CheckpointConfig.AudioVae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    EmbeddingsConnectorsPath = pipelineOptions.CheckpointConfig.Connectors,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.WanPipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    T5xxlPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    ClipVisionPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    HighNoiseDiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer2
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.MiniMaxVideoPipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    AudioVaePath = pipelineOptions.CheckpointConfig.AudioVae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.QwenImagePipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    LlmVisionPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path,
                    ModelArgs = "qwen_image_zero_cond_t=true" // TODO: should be optional
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.LensPipeline
             || pipelineOptions.Pipeline == PipelineType.MossImagePipeline)
            {
                return stableDiffusionOptions with
                {
                    TokenizerPath = pipelineOptions.CheckpointConfig.Tokenizer,
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.LladaImagePipeline)
            {
                return stableDiffusionOptions with
                {
                    TokenizerPath = pipelineOptions.CheckpointConfig.Tokenizer,
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    EmbeddingsConnectorsPath = pipelineOptions.CheckpointConfig.Connectors,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.IrisPipeline)
            {
                return stableDiffusionOptions with
                {
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.HiDreamPipeline
             || pipelineOptions.Pipeline == PipelineType.SenseNovaPipeline)
            {
                return stableDiffusionOptions with
                {
                    ModelPath = pipelineOptions.CheckpointConfig.FullCheckpoint
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.Fibo2Pipeline
             || pipelineOptions.Pipeline == PipelineType.MiniMaxImagePipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    LlmVisionPath = pipelineOptions.CheckpointConfig.TextEncoder2,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.AnimaPipeline
             || pipelineOptions.Pipeline == PipelineType.ErniePipeline
             || pipelineOptions.Pipeline == PipelineType.Flux2KleinPipeline
             || pipelineOptions.Pipeline == PipelineType.Flux3KleinPipeline
             || pipelineOptions.Pipeline == PipelineType.Krea2Pipeline
             || pipelineOptions.Pipeline == PipelineType.ZImagePipeline
             || pipelineOptions.Pipeline == PipelineType.BooguImagePipeline
             || pipelineOptions.Pipeline == PipelineType.LongCatImagePipeline)
            {
                return stableDiffusionOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }

            // Pipeline not supported
            throw new NotImplementedException(nameof(pipelineOptions.Pipeline));
        }


        /// <summary>
        /// Determines whether the pipeline is an upscale-only pipeline.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        internal static bool IsUpscalePipeline(this PipelineLoadOptions pipelineOptions)
        {
            return pipelineOptions.Pipeline == PipelineType.UpscalePipeline
                || IsVideoUpscalePipeline(pipelineOptions.Pipeline);
        }


        /// <summary>
        /// Determines whether the pipeline is a video upscaler, a diffusion model and VAE upscaling whole videos.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        internal static bool IsVideoUpscalePipeline(PipelineType pipeline)
        {
            return pipeline == PipelineType.SeedVR2Pipeline
                || pipeline == PipelineType.RelayVSRPipeline;
        }


        /// <summary>
        /// Creates the upscale options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.UpscaleOptions, or null if no upscaler is configured.</returns>
        internal static PipelineCommon.UpscaleOptions CreateUpscaleOptions(this PipelineCreateOptions options, PipelineLoadOptions pipelineOptions)
        {
            var upscaleConfig = pipelineOptions.UpscaleConfig;
            if (upscaleConfig == null)
                return null;

            var backendType = GetBackendType(options);
            var deviceId = backendType == Pipeline.SDBackendType.Vulkan
                ? pipelineOptions.DeviceId
                : pipelineOptions.DeviceVendorIndex;
            var upscaleOptions = new PipelineCommon.UpscaleOptions
            {
                Backend = $"*={backendType.GetShortName()}{deviceId}",
                ParamsBackend = GetParamsBackend(pipelineOptions),
                TileSize = upscaleConfig.TileSize,
                ScaleFactor = upscaleConfig.ScaleFactor
            };

            // Video upscaler, SeedVR2 or RelayVSR, per-component attention and convolution modes
            var executionOptions = pipelineOptions.ExecutionOptions;
            if (IsVideoUpscalePipeline(upscaleConfig.Pipeline))
            {
                return upscaleOptions with
                {
                    DiffusionModelPath = upscaleConfig.DiffusionModelPath,
                    VaePath = upscaleConfig.VaePath,
                    FlashAttnMode = executionOptions is null ? null : GetFlashAttention(pipelineOptions),
                    ConvDirectMode = executionOptions is null ? null : GetConvDirect(pipelineOptions)
                };
            }

            // Image upscaler, a single convolution mode, "on" enables it
            return upscaleOptions with
            {
                ModelPath = upscaleConfig.ModelPath,
                ConvDirectMode = executionOptions?.DiffusionConvDirect.GetShortName()
            };
        }


        /// <summary>
        /// Creates the upscale video options, zero values fall back to the defaults.
        /// </summary>
        /// <param name="seed">The seed.</param>
        /// <param name="windowSize">Size of the window.</param>
        /// <param name="temporalChunkSize">Size of the temporal chunk.</param>
        /// <returns>PipelineCommon.UpscaleVideoOptions.</returns>
        internal static PipelineCommon.UpscaleVideoOptions CreateUpscaleVideoOptions(long seed, int windowSize, int temporalChunkSize)
        {
            var defaultOptions = new PipelineCommon.UpscaleVideoOptions();
            return defaultOptions with
            {
                Seed = seed > 0 ? seed : defaultOptions.Seed,
                WindowSize = windowSize > 0 ? windowSize : defaultOptions.WindowSize,
                TemporalChunkSize = temporalChunkSize > 0 ? temporalChunkSize : defaultOptions.TemporalChunkSize
            };
        }


        /// <summary>
        /// Creates the GenerateImageOptions.
        /// </summary>
        /// <param name="defaultOptions">The default options.</param>
        /// <param name="generateOptions">The generate options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.GenerateImageOptions.</returns>
        internal static PipelineCommon.GenerateImageOptions CreateImageOptions(this PipelineCommon.GenerateImageOptions defaultOptions, GenerateImageOptions generateOptions, PipelineLoadOptions pipelineOptions)
        {
            var hiresDefaults = defaultOptions.Hires;
            var samplerDefaults = defaultOptions.SampleParameters;
            return defaultOptions with
            {
                Seed = generateOptions.Seed,
                Prompt = generateOptions.Prompt,
                NegativePrompt = generateOptions.NegativePrompt.DefaultIfWhiteSpace(),
                Width = generateOptions.Width,
                Height = generateOptions.Height,
                Strength = generateOptions.Strength,
                ControlStrength = generateOptions.ControlNetScale,
                Loras = GetLoraOptions(pipelineOptions.LoraAdapters, generateOptions.LoraOptions),
                InitImage = GetInitImage(generateOptions, pipelineOptions.ProcessType),
                RefImages = GetReferenceImages(generateOptions, pipelineOptions.ProcessType),
                ControlImage = GetControlNetImage(generateOptions, pipelineOptions.ProcessType),
                MaskImage = GetMaskImage(generateOptions, pipelineOptions.ProcessType),
                Hires = hiresDefaults.GetHiresOptions(generateOptions, pipelineOptions),
                SampleParameters = samplerDefaults with
                {
                    SampleSteps = generateOptions.Steps,
                    TxtCfg = GetTextGuidance(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    DistilledGuidance = GetDistilledGuidance(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    Eta = generateOptions.SchedulerOptions.Eta > 0 ? generateOptions.SchedulerOptions.Eta : samplerDefaults.Eta,
                    FlowShift = generateOptions.SchedulerOptions.FlowShift > 0 ? generateOptions.SchedulerOptions.FlowShift : samplerDefaults.FlowShift,
                    SampleMethod = GetSamplerType(generateOptions.SchedulerOptions),
                    Scheduler = GetSchedulerType(generateOptions.SchedulerOptions)
                },
                VaeTilingParameters = defaultOptions.VaeTilingParameters with
                {
                    Enabled = generateOptions.EnableVaeTiling
                }
            };
        }


        /// <summary>
        /// Creates the GenerateVideoOptions.
        /// </summary>
        /// <param name="defaultOptions">The default options.</param>
        /// <param name="generateOptions">The generate options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.GenerateVideoOptions.</returns>
        internal static PipelineCommon.GenerateVideoOptions CreateVideoOptions(this PipelineCommon.GenerateVideoOptions defaultOptions, GenerateVideoOptions generateOptions, PipelineLoadOptions pipelineOptions)
        {
            var hiresDefaults = defaultOptions.Hires;
            var samplerDefaults = defaultOptions.SampleParameters;
            var samplerHighNoiseDefaults = defaultOptions.HighNoiseSampleParameters;
            return defaultOptions with
            {
                Seed = generateOptions.Seed,
                Prompt = generateOptions.Prompt,
                NegativePrompt = generateOptions.NegativePrompt.DefaultIfWhiteSpace(),
                Width = generateOptions.Width,
                Height = generateOptions.Height,
                Strength = generateOptions.Strength,
                VideoFrames = generateOptions.Frames,
                Fps = (int)generateOptions.FrameRate,
                Loras = GetLoraOptions(pipelineOptions.LoraAdapters, generateOptions.LoraOptions),
                InitImage = GetFirstFrame(generateOptions, pipelineOptions.ProcessType),
                EndImage = GetLastFrame(generateOptions, pipelineOptions.ProcessType),
                ControlFrames = GetControlFrames(generateOptions, pipelineOptions.ProcessType),
                RefImages = GetReferenceImages(generateOptions, pipelineOptions.ProcessType),
                RefVideos = GetReferenceVideos(generateOptions, pipelineOptions.ProcessType),
                RefAudios = GetReferenceAudios(generateOptions, pipelineOptions.ProcessType),
                VaceStrength = generateOptions.ControlNetScale,
                Hires = hiresDefaults.GetHiresOptions(generateOptions, pipelineOptions),
                SampleParameters = samplerDefaults with
                {
                    SampleSteps = generateOptions.Steps,
                    TxtCfg = GetTextGuidance(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    DistilledGuidance = GetDistilledGuidance(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    Eta = generateOptions.SchedulerOptions.Eta > 0 ? generateOptions.SchedulerOptions.Eta : samplerDefaults.Eta,
                    FlowShift = generateOptions.SchedulerOptions.FlowShift > 0 ? generateOptions.SchedulerOptions.FlowShift : samplerDefaults.FlowShift,
                    SampleMethod = GetSamplerType(generateOptions.SchedulerOptions),
                    Scheduler = GetSchedulerType(generateOptions.SchedulerOptions),
                },
                HighNoiseSampleParameters = samplerHighNoiseDefaults with
                {
                    SampleSteps = generateOptions.Steps2,
                    TxtCfg = GetTextGuidanceHighNoise(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    DistilledGuidance = GetDistilledGuidanceHighNoise(generateOptions.GuidanceScale, generateOptions.GuidanceScale2, pipelineOptions.Pipeline),
                    Eta = generateOptions.SchedulerOptions.Eta > 0 ? generateOptions.SchedulerOptions.Eta : samplerDefaults.Eta,
                    FlowShift = generateOptions.SchedulerOptions.FlowShift > 0 ? generateOptions.SchedulerOptions.FlowShift : samplerDefaults.FlowShift,
                    SampleMethod = GetSamplerType(generateOptions.SchedulerOptions),
                    Scheduler = GetSchedulerType(generateOptions.SchedulerOptions),

                },
                VaeTilingParameters = defaultOptions.VaeTilingParameters with
                {
                    Enabled = generateOptions.EnableVaeTiling,
                    TemporalTiling = generateOptions.EnableVaeSlicing
                }
            };
        }


        /// <summary>
        /// Gets the type of the backend.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Pipeline.BackendType.</returns>
        /// <exception cref="System.Exception">Backend Not Found</exception>
        private static Pipeline.SDBackendType GetBackendType(PipelineCreateOptions options)
        {
            if (!Enum.TryParse(options.HostVersion, true, out Pipeline.SDBackendType backendType))
                throw new Exception($"{options.HostVersion} Backend Not Found.");

            return backendType;
        }


        /// <summary>
        /// Gets the backend.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <param name="backendType">Type of the backend.</param>
        private static string GetBackend(PipelineLoadOptions pipelineOptions, Pipeline.SDBackendType backendType)
        {
            var deviceId = backendType == Pipeline.SDBackendType.Vulkan
                ? pipelineOptions.DeviceId
                : pipelineOptions.DeviceVendorIndex;

            var diffusionDevice = $"{backendType.GetShortName()}{deviceId}";
            var autoEncoderDevice = pipelineOptions.ExecutionOptions.IsAutoEncoderCpuEnabled ? "cpu" : diffusionDevice;
            var textEncoderDevice = pipelineOptions.ExecutionOptions.IsTextEncoderCpuEnabled ? "cpu" : diffusionDevice;
            var controlnetDevice = pipelineOptions.ExecutionOptions.IsControlNetCpuEnabled ? "cpu" : diffusionDevice;
            return $"*={diffusionDevice},vae={autoEncoderDevice},te={textEncoderDevice},clip_vision={textEncoderDevice},audio_encoder={controlnetDevice},controlnet={autoEncoderDevice}";
        }


        /// <summary>
        /// Gets the parameters backend.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetParamsBackend(PipelineLoadOptions pipelineOptions)
        {
            if (pipelineOptions.MemoryMode == MemoryModeType.OffloadModel)
            {
                return "*=cpu,vae=cpu,te=cpu,clip_vision=cpu,audio_encoder=cpu,controlnet=cpu";
            }
            else if (pipelineOptions.MemoryMode == MemoryModeType.OffloadCPU)
            {
                return "*=cpu,vae=disk,te=disk,clip_vision=disk,audio_encoder=disk,controlnet=disk";
            }
            return string.Empty;
        }


        /// <summary>
        /// Gets the flash attention config.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetFlashAttention(PipelineLoadOptions pipelineOptions)
        {
            var options = pipelineOptions.ExecutionOptions;
            return $"diffusion={options.DiffusionFlashAttention.GetShortName()},vae={options.AutoEncoderFlashAttention.GetShortName()},te={options.TextEncoderFlashAttention.GetShortName()},clip_vision={options.TextEncoderFlashAttention.GetShortName()},audio_encoder={options.AutoEncoderFlashAttention.GetShortName()},controlnet={options.ControlNetFlashAttention.GetShortName()}";
        }


        /// <summary>
        /// Gets the conv direct config.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetConvDirect(PipelineLoadOptions pipelineOptions)
        {
            var options = pipelineOptions.ExecutionOptions;
            return $"diffusion={options.DiffusionConvDirect.GetShortName()},vae={options.AutoEncoderConvDirect.GetShortName()},controlnet={options.ControlNetConvDirect.GetShortName()}";
        }


        /// <summary>
        /// Gets the lora adapter options.
        /// </summary>
        /// <param name="loraAdapters">The lora adapters.</param>
        /// <param name="loraAdapterOptions">The lora adapter options.</param>
        private static PipelineCommon.LoraOptions[] GetLoraOptions(List<LoraConfig> loraAdapters, List<LoraOptions> loraAdapterOptions)
        {
            if (loraAdapterOptions.IsNullOrEmpty())
                return [];

            var loraParams = new List<PipelineCommon.LoraOptions>();
            foreach (var config in loraAdapters)
            {
                var options = loraAdapterOptions.FirstOrDefault(x => x.Name == config.Name);
                if (options == null)
                    continue;

                loraParams.Add(new PipelineCommon.LoraOptions
                {
                    Multiplier = options.Strength,
                    Path = Path.Combine(config.Path, config.Weights),
                });
            }
            return [.. loraParams];
        }


        /// <summary>
        /// Gets the Image Hires options.
        /// </summary>
        /// <param name="deafultOptions">The deafult options.</param>
        /// <param name="generateOptions">The generate options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static PipelineCommon.HiresOptions GetHiresOptions(this PipelineCommon.HiresOptions deafultOptions, GenerateImageOptions generateOptions, PipelineLoadOptions pipelineOptions)
        {
            if (generateOptions.LatentUpscale == LatentUpscale.Model || generateOptions.LatentUpscale == LatentUpscale.None)
                return deafultOptions;

            var upscaler = GetHiresUpscaleType(generateOptions.LatentUpscale);
            var tileSize = generateOptions.LatentUpscaleTileSize <= 0 ? 64 : generateOptions.LatentUpscaleTileSize;
            var steps = generateOptions.LatentUpscaleSteps <= 0 ? generateOptions.Steps / 2 : generateOptions.LatentUpscaleSteps;
            return deafultOptions with
            {
                Steps = steps,
                Enabled = true,
                Upscaler = upscaler,
                UpscaleTileSize = tileSize,
                DenoisingStrength = generateOptions.LatentUpscaleStrength,
                ModelPath = pipelineOptions.CheckpointConfig.LatentUpsampler
            };
        }


        /// <summary>
        /// Gets the Video Hires options.
        /// </summary>
        /// <param name="deafultOptions">The deafult options.</param>
        /// <param name="generateOptions">The generate options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.HiresOptions.</returns>
        private static PipelineCommon.HiresOptions GetHiresOptions(this PipelineCommon.HiresOptions deafultOptions, GenerateVideoOptions generateOptions, PipelineLoadOptions pipelineOptions)
        {
            if (generateOptions.LatentUpscale == LatentUpscale.None)
                return deafultOptions;

            var upscaler = GetHiresUpscaleType(generateOptions.LatentUpscale);
            var tileSize = generateOptions.LatentUpscaleTileSize <= 0 ? 64 : generateOptions.LatentUpscaleTileSize;
            var steps = generateOptions.LatentUpscaleSteps <= 0 ? generateOptions.Steps / 2 : generateOptions.LatentUpscaleSteps;
            return deafultOptions with
            {
                Steps = steps,
                Enabled = true,
                Upscaler = upscaler,
                UpscaleTileSize = tileSize,
                CustomSigmas = [0.85f, 0.725f, 0.421875f, 0.0f], // TODO: optional
                DenoisingStrength = generateOptions.LatentUpscaleStrength,
                ModelPath = pipelineOptions.CheckpointConfig.LatentUpsampler
            };
        }


        /// <summary>
        /// Gets the initial image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor GetInitImage(GenerateImageOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.ImageToImage || processType == ProcessType.ImageToImageControlNet || processType == ProcessType.ImageInpaint)
                return options.InputImages[0];

            return default;
        }


        /// <summary>
        /// Gets the control net image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor GetControlNetImage(GenerateImageOptions options, ProcessType processType)
        {
            if (options.InputControlImages.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.ImageControlNet || processType == ProcessType.ImageToImageControlNet)
                return options.InputControlImages[0];

            return default;
        }


        /// <summary>
        /// Gets the reference images.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor[] GetReferenceImages(GenerateImageOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.ImageEdit)
                return [.. options.InputImages];

            return default;
        }


        /// <summary>
        /// Gets the mask image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor GetMaskImage(GenerateImageOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (options.InputImages.Count < 2)
                return default;

            if (processType == ProcessType.ImageInpaint)
                return options.InputImages[1];

            return default;
        }


        /// <summary>
        /// Gets the first frame image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor GetFirstFrame(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (options.InputImages.Count > 2)
                return default;

            if (processType == ProcessType.ImageToVideo)
                return options.InputImages[0];

            return default;
        }


        /// <summary>
        /// Gets the last frame image.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor GetLastFrame(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (options.InputImages.Count != 2)
                return default;

            if (processType == ProcessType.ImageToVideo)
                return options.InputImages.Last();

            return default;
        }


        /// <summary>
        /// Gets the control frames.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor[] GetControlFrames(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputVideos.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.VideoToVideo)
                return [.. options.InputVideos[0].Frames];

            return default;
        }


        /// <summary>
        /// Gets the reference images.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static ImageTensor[] GetReferenceImages(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputImages.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.MediaToVideo)
                return [.. options.InputImages];

            return default;
        }


        /// <summary>
        /// Gets the reference videos.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static VideoSequence[] GetReferenceVideos(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputVideos.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.MediaToVideo)
                return [.. options.InputVideos];

            return default;
        }


        /// <summary>
        /// Gets the reference audios.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="processType">Type of the process.</param>
        private static AudioTensor[] GetReferenceAudios(GenerateVideoOptions options, ProcessType processType)
        {
            if (options.InputAudios.IsNullOrEmpty())
                return default;

            if (processType == ProcessType.MediaToVideo)
                return [.. options.InputAudios];

            return default;
        }


        /// <summary>
        /// Gets the text guidance.
        /// </summary>
        /// <param name="guidanceScale">The guidance scale.</param>
        /// <param name="guidanceScale2">The guidance scale2.</param>
        /// <param name="pipelineType">Type of the pipeline.</param>
        private static float GetTextGuidance(float guidanceScale, float guidanceScale2, PipelineType pipelineType)
        {
            if (pipelineType == PipelineType.WanPipeline)
                return Math.Max(1, guidanceScale2);
            return Math.Max(1, guidanceScale);
        }


        /// <summary>
        /// Gets the distilled guidance.
        /// </summary>
        /// <param name="guidanceScale">The guidance scale.</param>
        /// <param name="guidanceScale2">The guidance scale2.</param>
        /// <param name="pipelineType">Type of the pipeline.</param>
        private static float GetDistilledGuidance(float guidanceScale, float guidanceScale2, PipelineType pipelineType)
        {
            if (pipelineType == PipelineType.WanPipeline)
                return Math.Max(1, guidanceScale2);
            return Math.Max(1, guidanceScale2);
        }


        /// <summary>
        /// Gets the text guidance (high noise).
        /// </summary>
        /// <param name="guidanceScale">The guidance scale.</param>
        /// <param name="guidanceScale2">The guidance scale2.</param>
        /// <param name="pipelineType">Type of the pipeline.</param>
        private static float GetTextGuidanceHighNoise(float guidanceScale, float guidanceScale2, PipelineType pipelineType)
        {
            if (pipelineType == PipelineType.WanPipeline)
                return Math.Max(1, guidanceScale);
            return Math.Max(1, guidanceScale2);
        }


        /// <summary>
        /// Gets the distilled guidance (high noise).
        /// </summary>
        /// <param name="guidanceScale">The guidance scale.</param>
        /// <param name="guidanceScale2">The guidance scale2.</param>
        /// <param name="pipelineType">Type of the pipeline.</param>
        private static float GetDistilledGuidanceHighNoise(float guidanceScale, float guidanceScale2, PipelineType pipelineType)
        {
            if (pipelineType == PipelineType.WanPipeline)
                return Math.Max(1, guidanceScale);
            return Math.Max(1, guidanceScale);
        }


        /// <summary>
        /// Gets the type of the sampler.
        /// </summary>
        /// <param name="options">The options.</param>
        private static Pipeline.SamplerType GetSamplerType(SchedulerOptions options)
        {
            return options.Scheduler switch
            {
                SchedulerType.Euler => Pipeline.SamplerType.Euler,
                SchedulerType.EulerAncestral => Pipeline.SamplerType.Euler_A,
                SchedulerType.Heun => Pipeline.SamplerType.Heun,
                SchedulerType.DPM2 => Pipeline.SamplerType.DPM2,
                SchedulerType.DPMPlusPlus2SAncestral => Pipeline.SamplerType.DPMPP2S_A,
                SchedulerType.DPMPlusPlus2M => Pipeline.SamplerType.DPMPP2M,
                SchedulerType.DPMPlusPlus2Mv2 => Pipeline.SamplerType.DPMPP2Mv2,
                SchedulerType.IPNDM => Pipeline.SamplerType.IPNDM,
                SchedulerType.LCM => Pipeline.SamplerType.LCM,
                SchedulerType.DDIM => Pipeline.SamplerType.DDIM,
                SchedulerType.TCD => Pipeline.SamplerType.TCD,
                SchedulerType.ResidualMultistep => Pipeline.SamplerType.ResidualMultiStep,
                SchedulerType.Residual2S => Pipeline.SamplerType.Residual2S,
                SchedulerType.ERSDE => Pipeline.SamplerType.ER_SDE,
                SchedulerType.DPMPlusPlus2MSDE => Pipeline.SamplerType.DPMPP2M_SDE,
                SchedulerType.DPMPlusPlus2MSDEBT => Pipeline.SamplerType.DPMPP2M_SDE_BT,
                SchedulerType.LMS => Pipeline.SamplerType.LMS,
                _ => Pipeline.SamplerType.Default
            };
        }


        /// <summary>
        /// Gets the type of the scheduler.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Pipeline.SchedulerType.</returns>
        private static Pipeline.SchedulerType GetSchedulerType(SchedulerOptions options)
        {
            return options.SigmaScheduleType switch
            {
                SigmaScheduleType.Discrete => Pipeline.SchedulerType.Discrete,
                SigmaScheduleType.Karras => Pipeline.SchedulerType.Karras,
                SigmaScheduleType.Exponential => Pipeline.SchedulerType.Exponential,
                SigmaScheduleType.AYS => Pipeline.SchedulerType.AYS,
                SigmaScheduleType.GITS => Pipeline.SchedulerType.GITS,
                SigmaScheduleType.SGMUniform => Pipeline.SchedulerType.SGMUniform,
                SigmaScheduleType.Simple => Pipeline.SchedulerType.Simple,
                SigmaScheduleType.Smoothstep => Pipeline.SchedulerType.Smoothstep,
                SigmaScheduleType.KLOptimal => Pipeline.SchedulerType.KlOptimal,
                SigmaScheduleType.LCM => Pipeline.SchedulerType.LCM,
                SigmaScheduleType.BongTangent => Pipeline.SchedulerType.BongTangent,
                SigmaScheduleType.LTX2 => Pipeline.SchedulerType.LTX2,
                SigmaScheduleType.LogitNormal => Pipeline.SchedulerType.LogitNormal,
                SigmaScheduleType.Flux => Pipeline.SchedulerType.FLUX,
                SigmaScheduleType.Flux2 => Pipeline.SchedulerType.FLUX2,
                SigmaScheduleType.Beta => Pipeline.SchedulerType.Beta,
                SigmaScheduleType.LLADA => Pipeline.SchedulerType.LLADA,
                _ => Pipeline.SchedulerType.Default
            };
        }


        /// <summary>
        /// Gets the type of the Hires upscale.
        /// </summary>
        /// <param name="latentUpscaleType">Type of the latent upscale.</param>
        /// <returns>Pipeline.HiresUpscaleType.</returns>
        private static Pipeline.HiresUpscaleType GetHiresUpscaleType(LatentUpscale latentUpscaleType)
        {
            return latentUpscaleType switch
            {
                LatentUpscale.Lanczos => Pipeline.HiresUpscaleType.Lanczos,
                LatentUpscale.Latent => Pipeline.HiresUpscaleType.Latent,
                LatentUpscale.LatentAntialiased => Pipeline.HiresUpscaleType.LatentAntialiased,
                LatentUpscale.LatentBicubic => Pipeline.HiresUpscaleType.LatentBicubic,
                LatentUpscale.LatentBicubicAntialiased => Pipeline.HiresUpscaleType.LatentBicubicAntialiased,
                LatentUpscale.LatentNearest => Pipeline.HiresUpscaleType.LatentNearest,
                LatentUpscale.LatentNearestExact => Pipeline.HiresUpscaleType.LatentNearestExact,
                LatentUpscale.Model => Pipeline.HiresUpscaleType.Model,
                LatentUpscale.Nearest => Pipeline.HiresUpscaleType.Nearest,
                LatentUpscale.None => Pipeline.HiresUpscaleType.None,
                _ => Pipeline.HiresUpscaleType.Default
            };
        }


        /// <summary>
        /// Return default string if its only white space.
        /// </summary>
        /// <param name="value">The value.</param>
        private static string DefaultIfWhiteSpace(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            return value;
        }


        /// <summary>
        /// Translates the data type to the stable-diffusion.cpp weight type, unsupported types fall back to the model default.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        internal static Pipeline.DataType ToNativeDataType(this DataType dataType)
        {
            return dataType switch
            {
                // Float
                DataType.Float32 => Pipeline.DataType.F32,
                DataType.Float16 => Pipeline.DataType.F16,
                DataType.BFloat16 => Pipeline.DataType.BF16,
                DataType.Float8 => Pipeline.DataType.F8_E4M3,
                DataType.Float8E5M2 => Pipeline.DataType.F8_E5M2,
                DataType.Float64 => Pipeline.DataType.F64,

                // Integer
                DataType.Int8 => Pipeline.DataType.I8,
                DataType.Int16 => Pipeline.DataType.I16,
                DataType.Int32 => Pipeline.DataType.I32,
                DataType.Int64 => Pipeline.DataType.I64,

                // GGML, the quantization names match
                _ => Enum.TryParse<Pipeline.DataType>(dataType.ToString(), out var native) ? native : Pipeline.DataType.Default
            };
        }
    }
}
