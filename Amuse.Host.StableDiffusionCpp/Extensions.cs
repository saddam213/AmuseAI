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
        internal static PipelineCommon.ContextOptions CreateContextOptions(this PipelineCreateOptions options, PipelineLoadOptions pipelineOptions)
        {
            var backendType = GetBackendType(options);
            var backendDevice = GetBackend(pipelineOptions, backendType);
            var paramsBackend = GetParamsBackend(pipelineOptions, backendType);
            var contextOptions = new PipelineCommon.ContextOptions
            {
                Backend = backendDevice,
                ParamsBackend = paramsBackend,
                RngType = Pipeline.RngType.CPU,
                SamplerRngType = Pipeline.RngType.CPU,

                // Memory
                MaxVram = pipelineOptions.MemoryMode == MemoryModeType.Device ? "0" : "-1",
                DataType = GetDataType(pipelineOptions.QuantType, pipelineOptions.MemoryMode),
                AutoFit = pipelineOptions.MemoryMode == MemoryModeType.Balanced,
                EagerLoad = pipelineOptions.MemoryMode == MemoryModeType.Device,
                IsPrefetchEnabled = pipelineOptions.MemoryMode != MemoryModeType.Device,
                IsSegmentedComputeEnabled = pipelineOptions.MemoryMode != MemoryModeType.Device,

                // Misc
                ForceSdxlVaeConvScale = true,
                FlashAttn = pipelineOptions.IsFlashAttentionEnabled,
                DiffusionFlashAttn = pipelineOptions.IsFlashAttentionEnabled,
                LoraApplyMode = Pipeline.LoraApplyType.AtRuntime,
                PreviewType = Pipeline.PreviewType.Projection,

                // Vulkan Specific
                VaeConvDirect = backendType == Pipeline.BackendType.Vulkan,
                DiffusionConvDirect = backendType == Pipeline.BackendType.Vulkan,
            };


            if (pipelineOptions.Pipeline == PipelineType.StableDiffusionXLPipeline)
            {
                return contextOptions with
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
                return contextOptions with
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
                return contextOptions with
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
                return contextOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    T5xxlPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.IdeogramPipeline)
            {
                return contextOptions with
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
                return contextOptions with
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
                return contextOptions with
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
                return contextOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    AudioVaePath = pipelineOptions.CheckpointConfig.AudioVae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.QwenImagePipeline)
            {
                return contextOptions with
                {
                    VaePath = pipelineOptions.CheckpointConfig.Vae,
                    LlmPath = pipelineOptions.CheckpointConfig.TextEncoder,
                    DiffusionModelPath = pipelineOptions.CheckpointConfig.Transformer,
                    ControlNetPath = pipelineOptions.ControlNet?.Path,
                    ModelArgs = "qwen_image_zero_cond_t=true" // TODO: should be optional
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.HiDreamPipeline)
            {
                return contextOptions with
                {
                    ModelPath = pipelineOptions.CheckpointConfig.FullCheckpoint
                };
            }
            if (pipelineOptions.Pipeline == PipelineType.AnimaPipeline
             || pipelineOptions.Pipeline == PipelineType.ErniePipeline
             || pipelineOptions.Pipeline == PipelineType.Flux2KleinPipeline
             || pipelineOptions.Pipeline == PipelineType.Krea2Pipeline
             || pipelineOptions.Pipeline == PipelineType.ZImagePipeline
             || pipelineOptions.Pipeline == PipelineType.BooguImagePipeline
             || pipelineOptions.Pipeline == PipelineType.LensPipeline
             || pipelineOptions.Pipeline == PipelineType.LongCatImagePipeline)
            {
                return contextOptions with
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
        private static Pipeline.BackendType GetBackendType(PipelineCreateOptions options)
        {
            if (!Enum.TryParse(options.HostVersion, true, out Pipeline.BackendType backendType))
                throw new Exception($"{options.HostVersion} Backend Not Found.");

            return backendType;
        }


        /// <summary>
        /// Gets the backend.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <param name="backendType">Type of the backend.</param>
        private static string GetBackend(PipelineLoadOptions pipelineOptions, Pipeline.BackendType backendType)
        {
            var deviceId = backendType == Pipeline.BackendType.Vulkan
                ? pipelineOptions.DeviceId
                : pipelineOptions.DeviceVendorIndex;
            return $"{backendType.GetShortName()}{deviceId}";
        }


        /// <summary>
        /// Gets the parameters backend.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <param name="backendType">Type of the backend.</param>
        private static string GetParamsBackend(PipelineLoadOptions pipelineOptions, Pipeline.BackendType backendType)
        {
            if (pipelineOptions.MemoryMode == MemoryModeType.OffloadCPU || pipelineOptions.MemoryMode == MemoryModeType.OffloadModel)
            {
                return "*=cpu,vae=disk,te=disk,clip_vision=disk";
            }
            return string.Empty;
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
        /// Gets the DataType.
        /// </summary>
        /// <param name="quantType">Type of the quant.</param>
        /// <param name="memoryMode">The memory mode.</param>
        private static Pipeline.DataType GetDataType(QuantizationType quantType, MemoryModeType memoryMode)
        {
            if (memoryMode != MemoryModeType.Device)
                return Pipeline.DataType.Default;

            return quantType switch
            {
                QuantizationType.Q4Bit => Pipeline.DataType.Q4_0,
                QuantizationType.Q8Bit => Pipeline.DataType.Q8_0,
                QuantizationType.Q16Bit => Pipeline.DataType.BF16,
                _ => Pipeline.DataType.Default
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
    }
}
