using Amuse.Common;
using System;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using TensorStack.HuggingFace.Config;
using TensorStack.HuggingFace.Scheduler;

namespace Amuse.Host.HuggingFace
{
    public static class Extensions
    {
        public static EnvironmentConfig ToPythonOptions(this PipelineCreateOptions options)
        {
            return new EnvironmentConfig
            {
                IsDebug = options.IsDebug,
                Directory = options.Directory,
                Environment = options.Environment,
                PythonVersion = options.HostVersion,
                Requirements = options.Requirements?.ToArray(),
                Variables = options.Variables?.ToDictionary()
            };
        }


        public static TensorStack.HuggingFace.Common.PipelineReloadOptions ToPythonOptions(this Common.PipelineReloadOptions options)
        {
            return new TensorStack.HuggingFace.Common.PipelineReloadOptions
            {
                ProcessType = options.ProcessType.Cast<Common.ProcessType, TensorStack.HuggingFace.Common.ProcessType>(),
                ControlNet = options.ControlNet?.ToPythonOptions(),
                LoraAdapters = options.LoraAdapters?.Select(x => x.ToPythonOptions()).ToList()
            };
        }


        public static PipelineConfig ToPythonOptions(this PipelineLoadOptions options)
        {
            return new PipelineConfig
            {
                ModelPath = options.ModelPath,
                Template = options.Template,
                ModelType = options.ModelType,
                DataType = options.DataType.Cast<Amuse.Common.DataType, TensorStack.HuggingFace.Common.DataType>(),
                Device = options.Device,
                DeviceBusId = options.DeviceBusId,
                DeviceId = options.DeviceId,
                DeviceVendor = options.DeviceVendor,
                IsDeviceQuantizationEnabled = options.IsDeviceQuantizationEnabled,
                IsOptimizeDeviceEnabled = options.IsOptimizeDeviceEnabled,
                IsOptimizeChannelsEnabled = options.IsOptimizeChannelsEnabled,

                MemoryMode = options.MemoryMode.Cast<Amuse.Common.MemoryModeType, TensorStack.HuggingFace.Common.MemoryModeType>(),
                Pipeline = GetPipelineType(options).ToString(),
                ProcessType = options.ProcessType.Cast<Amuse.Common.ProcessType, TensorStack.HuggingFace.Common.ProcessType>(),
                QuantType = options.QuantType.Cast<Amuse.Common.QuantizationType, TensorStack.HuggingFace.Common.QuantizationType>(),
                Variant = options.Variant,

                ControlNet = options.ControlNet?.ToPythonOptions(),
                CheckpointConfig = options.CheckpointConfig?.ToPythonOptions(),
                LoraAdapters = options.LoraAdapters?.Select(x => x.ToPythonOptions()).ToList()
            };
        }


        public static TensorStack.HuggingFace.Common.GenerateImageOptions ToPythonOptions(this Common.GenerateImageOptions options)
        {
            return new TensorStack.HuggingFace.Common.GenerateImageOptions
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
                ControlNetScale = options.ControlNetScale,
                TempFileName = options.TempFileName,
                EnableVaeSlicing = options.EnableVaeSlicing,
                EnableVaeTiling = options.EnableVaeTiling,
                Language = options.Language.GetShortName(),
                Instruction = options.Instruction,
                Task = options.Task,
                MaxLength = options.MaxLength,
                MaxLength2 = options.MaxLength2,
                LoraOptions = options.LoraOptions?.Select(x => x.ToPythonOptions()).ToList(),
                SchedulerOptions = options.SchedulerOptions?.ToPythonOptions(),
                InputImages = options.InputImages,
                InputControlImages = options.InputControlImages
            };
        }


        public static TensorStack.HuggingFace.Common.GenerateVideoOptions ToPythonOptions(this Common.GenerateVideoOptions options)
        {
            return new TensorStack.HuggingFace.Common.GenerateVideoOptions
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
                ControlNetScale = options.ControlNetScale,
                TempFileName = options.TempFileName,
                FrameChunk = options.FrameChunk,
                FrameChunkOverlap = options.FrameChunkOverlap,
                NoiseCondition = options.NoiseCondition,
                EnableVaeSlicing = options.EnableVaeSlicing,
                EnableVaeTiling = options.EnableVaeTiling,
                Duration = options.Duration,
                Language = options.Language.GetShortName(),
                Instruction = options.Instruction,
                Task = options.Task,
                MaxLength = options.MaxLength,
                MaxLength2 = options.MaxLength2,
                LoraOptions = options.LoraOptions?.Select(x => x.ToPythonOptions()).ToList(),
                SchedulerOptions = options.SchedulerOptions?.ToPythonOptions(),
                InputImages = options.InputImages,
                InputAudios = options.InputAudios,
                InputControlImages = options.InputControlImages
            };
        }


        public static TensorStack.HuggingFace.Common.GenerateAudioOptions ToPythonOptions(this Common.GenerateAudioOptions options)
        {
            return new TensorStack.HuggingFace.Common.GenerateAudioOptions
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
                TempFileName = options.TempFileName,
                EnableVaeSlicing = options.EnableVaeSlicing,
                EnableVaeTiling = options.EnableVaeTiling,
                Duration = options.Duration,
                Language = options.Language.GetShortName(),
                Instruction = options.Instruction,
                MaxLength = options.MaxLength,
                MaxLength2 = options.MaxLength2,
                Bpm = options.Bpm,
                Keyscale = options.Keyscale,
                Task = options.Task,
                TrackName = options.TrackName,
                TimeSignature = options.TimeSignature,
                Speed = options.Speed,
                SilenceDuration = options.SilenceDuration,
                SampleRate = options.SampleRate,
                SchedulerOptions = options.SchedulerOptions?.ToPythonOptions(),
                LoraOptions = options.LoraOptions?.Select(x => x.ToPythonOptions()).ToList(),
                InputAudios = options.InputAudios
            };
        }


        public static TensorStack.HuggingFace.Common.GenerateTextOptions ToPythonOptions(this Common.GenerateTextOptions options)
        {
            return new TensorStack.HuggingFace.Common.GenerateTextOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Conversation = options.Conversation?.ToPythonOptions(),
                TempFileName = options.TempFileName,
                Language = options.Language.GetShortName(),
                Instruction = options.Instruction,
                Task = options.Task,
                MinLength = options.MinLength,
                MaxLength = options.MaxLength,
                IsSamplingEnabled = options.IsSamplingEnabled,
                Beams = options.Beams,
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                TopH = options.TopH,
                TypicalP = options.TypicalP,
                LengthPenalty = options.LengthPenalty,
                RepetitionPenalty = options.RepetitionPenalty,
                NoRepeatNgramSize = options.NoRepeatNgramSize,
                IsThinkingEnabled = options.IsThinkingEnabled,
                InputImages = options.InputImages,
                InputAudios = options.InputAudios,
                SampleRate = options.SampleRate,
                CacheType = options.CacheType.Cast<Amuse.Common.CacheType, TensorStack.HuggingFace.Common.CacheType>(),
            };
        }


        public static TensorStack.HuggingFace.Config.LoraConfig ToPythonOptions(this Common.LoraConfig config)
        {
            return new TensorStack.HuggingFace.Config.LoraConfig
            {
                Path = config.Path,
                Name = config.Name,
                Weights = config.Weights
            };
        }


        public static TensorStack.HuggingFace.Config.ControlNetConfig ToPythonOptions(this Common.ControlNetConfig config)
        {
            return new TensorStack.HuggingFace.Config.ControlNetConfig
            {
                Path = config.Path,
                Name = config.Name,
                Weights = config.Weights
            };
        }


        public static TensorStack.HuggingFace.Config.CheckpointConfig ToPythonOptions(this Common.CheckpointConfig config)
        {
            return new TensorStack.HuggingFace.Config.CheckpointConfig
            {
                TextEncoder = config.TextEncoder,
                TextEncoder2 = config.TextEncoder2,
                TextEncoder3 = config.TextEncoder3,
                Unet = config.Unet,
                Transformer = config.Transformer,
                Transformer2 = config.Transformer2,
                Vae = config.Vae,
                AudioVae = config.AudioVae,
                Vocoder = config.Vocoder,
                Connectors = config.Connectors,
                LatentUpsampler = config.LatentUpsampler,
                LatentUpsamplerTemporal = config.LatentUpsamplerTemporal,
                ConditionEncoder = config.ConditionEncoder,
                AudioTokenizer = config.AudioTokenizer,
                AudioDetokenizer = config.AudioDetokenizer,
            };
        }


        public static TensorStack.HuggingFace.Common.LoraOptions ToPythonOptions(this Common.LoraOptions options)
        {
            return new TensorStack.HuggingFace.Common.LoraOptions
            {
                Name = options.Name,
                Strength = options.Strength,
            };
        }


        public static Common.PipelineProgress ToProgress(this TensorStack.HuggingFace.Common.PipelineProgress pipelineProgress)
        {
            return new Common.PipelineProgress
            {
                Key = pipelineProgress.Key,
                Subkey = pipelineProgress.Subkey,
                ElapsedKey = pipelineProgress.ElapsedKey,
                Timestamp = pipelineProgress.Timestamp,
                Elapsed = pipelineProgress.Elapsed,
                Value = pipelineProgress.Value,
                Maximum = pipelineProgress.Maximum,
                BatchValue = pipelineProgress.BatchValue,
                BatchMaximum = pipelineProgress.BatchMaximum,
                Message = pipelineProgress.Message,
                Tensors = pipelineProgress.Tensor == null ? null : [pipelineProgress.Tensor]
            };
        }


        public static TensorStack.HuggingFace.Scheduler.SchedulerOptions ToPythonOptions(this Common.SchedulerOptions options)
        {
            return options.Scheduler switch
            {
                Amuse.Common.SchedulerType.LMS => new LMSOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.Euler => new EulerOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                    SigmaMax = options.SigmaMax > 0 ? options.SigmaMax : null,
                    SigmaMin = options.SigmaMin > 0 ? options.SigmaMin : null,
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    InterpolationType = options.InterpolationType.Cast<Amuse.Common.InterpolationType, TensorStack.HuggingFace.Scheduler.InterpolationType>(),
                    TimestepType = options.TimestepType.Cast<Amuse.Common.TimestepType, TensorStack.HuggingFace.Scheduler.TimestepType>(),
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.EulerAncestral => new EulerAncestralOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.DDPM => new DDPMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    VarianceType = options.VarianceType.Cast<Amuse.Common.VarianceType, TensorStack.HuggingFace.Scheduler.VarianceType>(),
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.DDIM => new DDIMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SetAlphaToOne = options.SetAlphaToOne,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.KDPM2 => new KDPM2Options
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.KDPM2Ancestral => new KDPM2AncestralOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.DDPMWuerstchen => new DDPMWuerstchenOptions
                {
                    S = options.SValue,
                    Scaler = options.Scaler,
                },
                Amuse.Common.SchedulerType.LCM => new LCMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    OriginalInferenceSteps = options.OriginalInferenceSteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SetAlphaToOne = options.SetAlphaToOne,
                    TimestepScaling = options.TimestepScaling,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.FlowMatchEuler => new FlowMatchEulerOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    Shift = options.Shift,
                    BaseShift = options.BaseShift,
                    MaxShift = options.MaxShift,
                    ShiftTerminal = options.ShiftTerminal > 0 ? options.ShiftTerminal : null,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    BaseImageSeqLen = options.BaseImageSeqLen,
                    MaxImageSeqLen = options.MaxImageSeqLen,
                    InvertSigmas = options.InvertSigmas,
                    StochasticSampling = options.StochasticSampling,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.FlowMatchHeun => new FlowMatchHeunOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    Shift = options.Shift,
                },
                Amuse.Common.SchedulerType.PNDM => new PNDMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    SetAlphaToOne = options.SetAlphaToOne,
                    SkipPrkSteps = options.SkipPrkSteps,
                },
                Amuse.Common.SchedulerType.Heun => new HeunOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.UniPCMultistep => new UniPCMultistepOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    SigmaMin = options.SigmaMin > 0 ? options.SigmaMin : null,
                    SigmaMax = options.SigmaMax > 0 ? options.SigmaMax : null,
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    SolverOrder = options.SolverOrder,
                    LowerOrderFinal = options.LowerOrderFinal,
                    ShiftTerminal = options.ShiftTerminal > 0 ? options.ShiftTerminal : null,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    FlowShift = options.FlowShift,
                    PredictX0 = options.PredictX0,
                    UseFlowSigmas = options.UseFlowSigmas,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.DPMSolverMultistep => new DPMSolverMultistepOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    SolverOrder = options.SolverOrder,
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    LowerOrderFinal = options.LowerOrderFinal,
                    FlowShift = options.FlowShift,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    UseFlowSigmas = options.UseFlowSigmas,
                    EulerAtFinal = options.EulerAtFinal,
                    AlgorithmType = options.AlgorithmType.Cast<Amuse.Common.AlgorithmType, TensorStack.HuggingFace.Scheduler.AlgorithmType>(),
                    UseLuLambdas = options.UseLuLambdas,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.DPMSolverSinglestep => new DPMSolverSinglestepOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    SolverOrder = options.SolverOrder,
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    LowerOrderFinal = options.LowerOrderFinal,
                    FlowShift = options.FlowShift,
                    VarianceType = options.VarianceType.Cast<Amuse.Common.VarianceType, TensorStack.HuggingFace.Scheduler.VarianceType>(),
                    AlgorithmType = options.AlgorithmType.Cast<Amuse.Common.AlgorithmType, TensorStack.HuggingFace.Scheduler.AlgorithmType>(),
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    UseFlowSigmas = options.UseFlowSigmas,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.DPMSolverSDE => new DPMSolverSDEOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                    NoiseSamplerSeed = options.NoiseSamplerSeed,
                },
                Amuse.Common.SchedulerType.DEISMultistep => new DEISMultistepOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    SolverOrder = options.SolverOrder,
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    LowerOrderFinal = options.LowerOrderFinal,
                    FlowShift = options.FlowShift,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    AlgorithmType = options.AlgorithmType.Cast<Amuse.Common.AlgorithmType, TensorStack.HuggingFace.Scheduler.AlgorithmType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    UseFlowSigmas = options.UseFlowSigmas,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.EDMEuler => new EDMEulerOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    SigmaMin = options.SigmaMin ?? 0,
                    SigmaMax = options.SigmaMax ?? 0,
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    Rho = options.Rho,
                    SigmaData = options.SigmaData,
                    SigmaScheduleType = options.SigmaScheduleType.AsPythonType(),
                },
                Amuse.Common.SchedulerType.EDMDPMSolverMultistep => new EDMDPMSolverMultistepOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    AlgorithmType = options.AlgorithmType.Cast<Amuse.Common.AlgorithmType, TensorStack.HuggingFace.Scheduler.AlgorithmType>(),
                    EulerAtFinal = options.EulerAtFinal,
                    SigmaMin = options.SigmaMin ?? 0,
                    SigmaMax = options.SigmaMax ?? 0,
                    FinalSigmasType = options.FinalSigmasType.Cast<Amuse.Common.FinalSigmasType, TensorStack.HuggingFace.Scheduler.FinalSigmasType>(),
                    Rho = options.Rho,
                    SigmaData = options.SigmaData,
                    SigmaScheduleType = options.SigmaScheduleType.AsPythonType(),
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    SolverOrder = options.SolverOrder,
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    LowerOrderFinal = options.LowerOrderFinal,
                },
                Amuse.Common.SchedulerType.FlowMatchLCM => new FlowMatchLCMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    Shift = options.Shift,
                    BaseShift = options.BaseShift,
                    MaxShift = options.MaxShift,
                    ShiftTerminal = options.ShiftTerminal > 0 ? options.ShiftTerminal : null,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    UpscaleMode = options.UpscaleMode.Cast<Amuse.Common.UpscaleModeType, TensorStack.HuggingFace.Scheduler.UpscaleModeType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    InvertSigmas = options.InvertSigmas,
                    BaseImageSeqLen = options.BaseImageSeqLen,     // TODO
                    MaxImageSeqLen = options.MaxImageSeqLen,       // TODO
                    ScaleFactors = options.ScaleFactors,           // TODO
                    UseKarrasSigmas = options.UseKarrasSigmas,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                },
                Amuse.Common.SchedulerType.IPNDM => new IPNDMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                },
                Amuse.Common.SchedulerType.CogVideoXDDIM => new CogVideoXDDIMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    SetAlphaToOne = options.SetAlphaToOne,
                    SNRShiftScale = options.SNRShiftScale,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.CogVideoXDPM => new CogVideoXDPMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    SetAlphaToOne = options.SetAlphaToOne,
                    SNRShiftScale = options.SNRShiftScale,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.Helios => new HeliosOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    Shift = options.Shift,
                    Gamma = options.Gamma,
                    SolverOrder = options.SolverOrder,
                    SolverType = options.SolverType.Cast<Amuse.Common.SolverType, TensorStack.HuggingFace.Scheduler.SolverType>(),
                    LowerOrderFinal = options.LowerOrderFinal,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    UseFlowSigmas = options.UseFlowSigmas,
                    PredictX0 = options.PredictX0,
                    Thresholding = options.Thresholding,
                    Stages = options.Stages,                            // TODO
                    StageRange = options.StageRange,                    // TODO
                    DisableCorrector = options.DisableCorrector,        // TODO
                },
                Amuse.Common.SchedulerType.HeliosDMD => new HeliosDMDOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    Shift = options.Shift,
                    Gamma = options.Gamma,
                    TimeShiftType = options.TimeShiftType.Cast<Amuse.Common.TimeShiftType, TensorStack.HuggingFace.Scheduler.TimeShiftType>(),
                    UseDynamicShifting = options.UseDynamicShifting,
                    UseFlowSigmas = options.UseFlowSigmas,
                    Stages = options.Stages,                            // TODO
                    StageRange = options.StageRange,                    // TODO
                },
                Amuse.Common.SchedulerType.TCD => new TCDOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    TimestepScaling = options.TimestepScaling,
                    ClipSample = options.ClipSample,
                    ClipSampleRange = options.ClipSampleRange,
                    SampleMaxValue = options.SampleMaxValue,
                    OriginalInferenceSteps = options.OriginalInferenceSteps,
                    SetAlphaToOne = options.SetAlphaToOne,
                    RescaleBetasZeroSNR = options.RescaleBetasZeroSNR,
                },
                Amuse.Common.SchedulerType.SCM => new SCMOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    SigmaData = options.SigmaData,
                },
                Amuse.Common.SchedulerType.SASolver => new SASolverOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    BetaEnd = options.BetaEnd,
                    BetaStart = options.BetaStart,
                    BetaSchedule = options.BetaSchedule.Cast<Amuse.Common.BetaScheduleType, TensorStack.HuggingFace.Scheduler.BetaScheduleType>(),
                    PredictionType = options.PredictionType.Cast<Amuse.Common.PredictionType, TensorStack.HuggingFace.Scheduler.PredictionType>(),
                    TimestepSpacing = options.TimestepSpacing.Cast<Amuse.Common.TimestepSpacingType, TensorStack.HuggingFace.Scheduler.TimestepSpacingType>(),
                    StepsOffset = options.StepsOffset,
                    Thresholding = options.Thresholding,
                    DynamicThresholdingRatio = options.DynamicThresholdingRatio,
                    SampleMaxValue = options.SampleMaxValue,
                    FlowShift = options.FlowShift,
                    AlgorithmType = options.AlgorithmType.Cast<Amuse.Common.AlgorithmType, TensorStack.HuggingFace.Scheduler.AlgorithmType>(),
                    VarianceType = options.VarianceType.Cast<Amuse.Common.VarianceType, TensorStack.HuggingFace.Scheduler.VarianceType>(),
                    UseFlowSigmas = options.UseFlowSigmas,
                    LowerOrderFinal = options.LowerOrderFinal,
                    PredictorOrder = options.PredictorOrder,
                    CorrectorOrder = options.CorrectorOrder,
                    UseBetaSigmas = options.UseBetaSigmas,
                    UseExponentialSigmas = options.UseExponentialSigmas,
                    UseKarrasSigmas = options.UseKarrasSigmas,
                },
                Amuse.Common.SchedulerType.LTXEulerAncestral => new LTXEulerAncestralRFOptions
                {
                    NumTrainTimesteps = options.NumTrainTimesteps,
                    Eta = options.Eta,
                    SNoise = options.SNoise,
                },
                _ => throw new NotImplementedException(),
            };
        }

        public static Common.SchedulerOptions ToClientOptions(this TensorStack.HuggingFace.Scheduler.SchedulerOptions options)
        {
            if (options is TensorStack.HuggingFace.Scheduler.LMSOptions lmsOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = lmsOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = lmsOptions.NumTrainTimesteps,
                    BetaEnd = lmsOptions.BetaEnd,
                    BetaStart = lmsOptions.BetaStart,
                    BetaSchedule = lmsOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = lmsOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = lmsOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = lmsOptions.StepsOffset,
                    UseKarrasSigmas = lmsOptions.UseKarrasSigmas,
                    UseBetaSigmas = lmsOptions.UseBetaSigmas,
                    UseExponentialSigmas = lmsOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.EulerOptions eulerOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = eulerOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = eulerOptions.NumTrainTimesteps,
                    BetaEnd = eulerOptions.BetaEnd,
                    BetaStart = eulerOptions.BetaStart,
                    BetaSchedule = eulerOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = eulerOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = eulerOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = eulerOptions.StepsOffset,
                    UseKarrasSigmas = eulerOptions.UseKarrasSigmas,
                    UseBetaSigmas = eulerOptions.UseBetaSigmas,
                    UseExponentialSigmas = eulerOptions.UseExponentialSigmas,
                    SigmaMax = eulerOptions.SigmaMax ?? 0,
                    SigmaMin = eulerOptions.SigmaMin ?? 0,
                    FinalSigmasType = eulerOptions.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    InterpolationType = eulerOptions.InterpolationType.Cast<TensorStack.HuggingFace.Scheduler.InterpolationType, Common.InterpolationType>(),
                    TimestepType = eulerOptions.TimestepType.Cast<TensorStack.HuggingFace.Scheduler.TimestepType, Common.TimestepType>(),
                    RescaleBetasZeroSNR = eulerOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.EulerAncestralOptions eulerAncestralOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = eulerAncestralOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = eulerAncestralOptions.NumTrainTimesteps,
                    BetaEnd = eulerAncestralOptions.BetaEnd,
                    BetaStart = eulerAncestralOptions.BetaStart,
                    BetaSchedule = eulerAncestralOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = eulerAncestralOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = eulerAncestralOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = eulerAncestralOptions.StepsOffset,
                    RescaleBetasZeroSNR = eulerAncestralOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DDPMOptions ddpmOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = ddpmOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = ddpmOptions.NumTrainTimesteps,
                    BetaEnd = ddpmOptions.BetaEnd,
                    BetaStart = ddpmOptions.BetaStart,
                    BetaSchedule = ddpmOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = ddpmOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = ddpmOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = ddpmOptions.StepsOffset,
                    ClipSample = ddpmOptions.ClipSample,
                    ClipSampleRange = ddpmOptions.ClipSampleRange,
                    SampleMaxValue = ddpmOptions.SampleMaxValue,
                    Thresholding = ddpmOptions.Thresholding,
                    DynamicThresholdingRatio = ddpmOptions.DynamicThresholdingRatio,
                    VarianceType = ddpmOptions.VarianceType.Cast<TensorStack.HuggingFace.Scheduler.VarianceType, Common.VarianceType>(),
                    RescaleBetasZeroSNR = ddpmOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DDIMOptions ddimOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = ddimOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = ddimOptions.NumTrainTimesteps,
                    BetaEnd = ddimOptions.BetaEnd,
                    BetaStart = ddimOptions.BetaStart,
                    BetaSchedule = ddimOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = ddimOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = ddimOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = ddimOptions.StepsOffset,
                    ClipSample = ddimOptions.ClipSample,
                    ClipSampleRange = ddimOptions.ClipSampleRange,
                    SampleMaxValue = ddimOptions.SampleMaxValue,
                    Thresholding = ddimOptions.Thresholding,
                    DynamicThresholdingRatio = ddimOptions.DynamicThresholdingRatio,
                    SetAlphaToOne = ddimOptions.SetAlphaToOne,
                    RescaleBetasZeroSNR = ddimOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.KDPM2Options kdpm2Options)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = kdpm2Options.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = kdpm2Options.NumTrainTimesteps,
                    BetaEnd = kdpm2Options.BetaEnd,
                    BetaStart = kdpm2Options.BetaStart,
                    BetaSchedule = kdpm2Options.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = kdpm2Options.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = kdpm2Options.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = kdpm2Options.StepsOffset,
                    UseKarrasSigmas = kdpm2Options.UseKarrasSigmas,
                    UseBetaSigmas = kdpm2Options.UseBetaSigmas,
                    UseExponentialSigmas = kdpm2Options.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.KDPM2AncestralOptions kdpm2AncestralOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = kdpm2AncestralOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = kdpm2AncestralOptions.NumTrainTimesteps,
                    BetaEnd = kdpm2AncestralOptions.BetaEnd,
                    BetaStart = kdpm2AncestralOptions.BetaStart,
                    BetaSchedule = kdpm2AncestralOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = kdpm2AncestralOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = kdpm2AncestralOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = kdpm2AncestralOptions.StepsOffset,
                    UseKarrasSigmas = kdpm2AncestralOptions.UseKarrasSigmas,
                    UseBetaSigmas = kdpm2AncestralOptions.UseBetaSigmas,
                    UseExponentialSigmas = kdpm2AncestralOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DDPMWuerstchenOptions ddpmWuerstchenOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = ddpmWuerstchenOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    SValue = ddpmWuerstchenOptions.S,
                    Scaler = ddpmWuerstchenOptions.Scaler,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.LCMOptions lcmOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = lcmOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = lcmOptions.NumTrainTimesteps,
                    OriginalInferenceSteps = lcmOptions.OriginalInferenceSteps,
                    BetaEnd = lcmOptions.BetaEnd,
                    BetaStart = lcmOptions.BetaStart,
                    BetaSchedule = lcmOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = lcmOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = lcmOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = lcmOptions.StepsOffset,
                    ClipSample = lcmOptions.ClipSample,
                    ClipSampleRange = lcmOptions.ClipSampleRange,
                    SampleMaxValue = lcmOptions.SampleMaxValue,
                    Thresholding = lcmOptions.Thresholding,
                    DynamicThresholdingRatio = lcmOptions.DynamicThresholdingRatio,
                    SetAlphaToOne = lcmOptions.SetAlphaToOne,
                    TimestepScaling = lcmOptions.TimestepScaling,
                    RescaleBetasZeroSNR = lcmOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.FlowMatchEulerOptions flowMatchEulerOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = flowMatchEulerOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = flowMatchEulerOptions.NumTrainTimesteps,
                    Shift = flowMatchEulerOptions.Shift,
                    BaseShift = flowMatchEulerOptions.BaseShift ?? 0,
                    MaxShift = flowMatchEulerOptions.MaxShift ?? 0,
                    ShiftTerminal = flowMatchEulerOptions.ShiftTerminal,
                    TimeShiftType = flowMatchEulerOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    UseDynamicShifting = flowMatchEulerOptions.UseDynamicShifting,
                    BaseImageSeqLen = flowMatchEulerOptions.BaseImageSeqLen,
                    MaxImageSeqLen = flowMatchEulerOptions.MaxImageSeqLen,
                    InvertSigmas = flowMatchEulerOptions.InvertSigmas,
                    StochasticSampling = flowMatchEulerOptions.StochasticSampling,
                    UseKarrasSigmas = flowMatchEulerOptions.UseKarrasSigmas,
                    UseBetaSigmas = flowMatchEulerOptions.UseBetaSigmas,
                    UseExponentialSigmas = flowMatchEulerOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.FlowMatchHeunOptions flowMatchHeunOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = flowMatchHeunOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = flowMatchHeunOptions.NumTrainTimesteps,
                    Shift = flowMatchHeunOptions.Shift,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.PNDMOptions pndmOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = pndmOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = pndmOptions.NumTrainTimesteps,
                    BetaEnd = pndmOptions.BetaEnd,
                    BetaStart = pndmOptions.BetaStart,
                    BetaSchedule = pndmOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = pndmOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = pndmOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = pndmOptions.StepsOffset,
                    SetAlphaToOne = pndmOptions.SetAlphaToOne,
                    SkipPrkSteps = pndmOptions.SkipPrkSteps,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.HeunOptions heunOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = heunOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = heunOptions.NumTrainTimesteps,
                    BetaEnd = heunOptions.BetaEnd,
                    BetaStart = heunOptions.BetaStart,
                    BetaSchedule = heunOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = heunOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = heunOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = heunOptions.StepsOffset,
                    ClipSample = heunOptions.ClipSample,
                    ClipSampleRange = heunOptions.ClipSampleRange,
                    UseKarrasSigmas = heunOptions.UseKarrasSigmas,
                    UseBetaSigmas = heunOptions.UseBetaSigmas,
                    UseExponentialSigmas = heunOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.UniPCMultistepOptions unipcMultistepOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = unipcMultistepOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = unipcMultistepOptions.NumTrainTimesteps,
                    BetaEnd = unipcMultistepOptions.BetaEnd,
                    BetaStart = unipcMultistepOptions.BetaStart,
                    BetaSchedule = unipcMultistepOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = unipcMultistepOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = unipcMultistepOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = unipcMultistepOptions.StepsOffset,
                    Thresholding = unipcMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = unipcMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = unipcMultistepOptions.SampleMaxValue,
                    SigmaMin = unipcMultistepOptions.SigmaMin ?? 0,
                    SigmaMax = unipcMultistepOptions.SigmaMax ?? 0,
                    FinalSigmasType = unipcMultistepOptions.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    SolverType = unipcMultistepOptions.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    SolverOrder = unipcMultistepOptions.SolverOrder,
                    LowerOrderFinal = unipcMultistepOptions.LowerOrderFinal,
                    ShiftTerminal = unipcMultistepOptions.ShiftTerminal,
                    TimeShiftType = unipcMultistepOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    UseDynamicShifting = unipcMultistepOptions.UseDynamicShifting,
                    FlowShift = unipcMultistepOptions.FlowShift,
                    PredictX0 = unipcMultistepOptions.PredictX0,
                    UseFlowSigmas = unipcMultistepOptions.UseFlowSigmas,
                    UseKarrasSigmas = unipcMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = unipcMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = unipcMultistepOptions.UseExponentialSigmas,
                    RescaleBetasZeroSNR = unipcMultistepOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DPMSolverMultistepOptions dpmSolverMultistepOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = dpmSolverMultistepOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = dpmSolverMultistepOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverMultistepOptions.BetaEnd,
                    BetaStart = dpmSolverMultistepOptions.BetaStart,
                    BetaSchedule = dpmSolverMultistepOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = dpmSolverMultistepOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = dpmSolverMultistepOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = dpmSolverMultistepOptions.StepsOffset,
                    Thresholding = dpmSolverMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = dpmSolverMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = dpmSolverMultistepOptions.SampleMaxValue,
                    SolverOrder = dpmSolverMultistepOptions.SolverOrder,
                    SolverType = dpmSolverMultistepOptions.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    LowerOrderFinal = dpmSolverMultistepOptions.LowerOrderFinal,
                    FlowShift = dpmSolverMultistepOptions.FlowShift,
                    TimeShiftType = dpmSolverMultistepOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    FinalSigmasType = dpmSolverMultistepOptions.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    UseDynamicShifting = dpmSolverMultistepOptions.UseDynamicShifting,
                    UseFlowSigmas = dpmSolverMultistepOptions.UseFlowSigmas,
                    EulerAtFinal = dpmSolverMultistepOptions.EulerAtFinal,
                    AlgorithmType = dpmSolverMultistepOptions.AlgorithmType.Cast<TensorStack.HuggingFace.Scheduler.AlgorithmType, Common.AlgorithmType>(),
                    UseLuLambdas = dpmSolverMultistepOptions.UseLuLambdas,
                    UseKarrasSigmas = dpmSolverMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverMultistepOptions.UseExponentialSigmas,
                    RescaleBetasZeroSNR = dpmSolverMultistepOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DPMSolverSinglestepOptions dpmSolverSinglestepOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = dpmSolverSinglestepOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = dpmSolverSinglestepOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverSinglestepOptions.BetaEnd,
                    BetaStart = dpmSolverSinglestepOptions.BetaStart,
                    BetaSchedule = dpmSolverSinglestepOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = dpmSolverSinglestepOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = dpmSolverSinglestepOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = dpmSolverSinglestepOptions.StepsOffset,
                    Thresholding = dpmSolverSinglestepOptions.Thresholding,
                    DynamicThresholdingRatio = dpmSolverSinglestepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = dpmSolverSinglestepOptions.SampleMaxValue,
                    SolverOrder = dpmSolverSinglestepOptions.SolverOrder,
                    SolverType = dpmSolverSinglestepOptions.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    LowerOrderFinal = dpmSolverSinglestepOptions.LowerOrderFinal,
                    FlowShift = dpmSolverSinglestepOptions.FlowShift,
                    VarianceType = dpmSolverSinglestepOptions.VarianceType.Cast<TensorStack.HuggingFace.Scheduler.VarianceType, Common.VarianceType>(),
                    AlgorithmType = dpmSolverSinglestepOptions.AlgorithmType.Cast<TensorStack.HuggingFace.Scheduler.AlgorithmType, Common.AlgorithmType>(),
                    FinalSigmasType = dpmSolverSinglestepOptions.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    UseFlowSigmas = dpmSolverSinglestepOptions.UseFlowSigmas,
                    UseKarrasSigmas = dpmSolverSinglestepOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverSinglestepOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverSinglestepOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DPMSolverSDEOptions dpmSolverSDEOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = dpmSolverSDEOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = dpmSolverSDEOptions.NumTrainTimesteps,
                    BetaEnd = dpmSolverSDEOptions.BetaEnd,
                    BetaStart = dpmSolverSDEOptions.BetaStart,
                    BetaSchedule = dpmSolverSDEOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = dpmSolverSDEOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = dpmSolverSDEOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = dpmSolverSDEOptions.StepsOffset,
                    UseKarrasSigmas = dpmSolverSDEOptions.UseKarrasSigmas,
                    UseBetaSigmas = dpmSolverSDEOptions.UseBetaSigmas,
                    UseExponentialSigmas = dpmSolverSDEOptions.UseExponentialSigmas,
                    NoiseSamplerSeed = dpmSolverSDEOptions.NoiseSamplerSeed,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.DEISMultistepOptions deisMultistepOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = deisMultistepOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = deisMultistepOptions.NumTrainTimesteps,
                    BetaEnd = deisMultistepOptions.BetaEnd,
                    BetaStart = deisMultistepOptions.BetaStart,
                    BetaSchedule = deisMultistepOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = deisMultistepOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = deisMultistepOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = deisMultistepOptions.StepsOffset,
                    Thresholding = deisMultistepOptions.Thresholding,
                    DynamicThresholdingRatio = deisMultistepOptions.DynamicThresholdingRatio,
                    SampleMaxValue = deisMultistepOptions.SampleMaxValue,
                    SolverOrder = deisMultistepOptions.SolverOrder,
                    SolverType = deisMultistepOptions.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    LowerOrderFinal = deisMultistepOptions.LowerOrderFinal,
                    FlowShift = deisMultistepOptions.FlowShift,
                    TimeShiftType = deisMultistepOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    AlgorithmType = deisMultistepOptions.AlgorithmType.Cast<TensorStack.HuggingFace.Scheduler.AlgorithmType, Common.AlgorithmType>(),
                    UseDynamicShifting = deisMultistepOptions.UseDynamicShifting,
                    UseFlowSigmas = deisMultistepOptions.UseFlowSigmas,
                    UseKarrasSigmas = deisMultistepOptions.UseKarrasSigmas,
                    UseBetaSigmas = deisMultistepOptions.UseBetaSigmas,
                    UseExponentialSigmas = deisMultistepOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.EDMEulerOptions edmEulerOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = edmEulerOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = edmEulerOptions.NumTrainTimesteps,
                    PredictionType = edmEulerOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    SigmaMax = edmEulerOptions.SigmaMax,
                    SigmaMin = edmEulerOptions.SigmaMin,
                    FinalSigmasType = edmEulerOptions.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    Rho = edmEulerOptions.Rho,
                    SigmaData = edmEulerOptions.SigmaData,
                    SigmaScheduleType = edmEulerOptions.SigmaScheduleType.Cast<TensorStack.HuggingFace.Scheduler.SigmaScheduleType, Common.SigmaScheduleType>(),
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.EDMDPMSolverMultistepOptions edmDPMSolverMultistep)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = edmDPMSolverMultistep.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = edmDPMSolverMultistep.NumTrainTimesteps,
                    PredictionType = edmDPMSolverMultistep.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    AlgorithmType = edmDPMSolverMultistep.AlgorithmType.Cast<TensorStack.HuggingFace.Scheduler.AlgorithmType, Common.AlgorithmType>(),
                    EulerAtFinal = edmDPMSolverMultistep.EulerAtFinal,
                    SigmaMax = edmDPMSolverMultistep.SigmaMax,
                    SigmaMin = edmDPMSolverMultistep.SigmaMin,
                    FinalSigmasType = edmDPMSolverMultistep.FinalSigmasType.Cast<TensorStack.HuggingFace.Scheduler.FinalSigmasType, Common.FinalSigmasType>(),
                    Rho = edmDPMSolverMultistep.Rho,
                    SigmaData = edmDPMSolverMultistep.SigmaData,
                    SigmaScheduleType = edmDPMSolverMultistep.SigmaScheduleType.Cast<TensorStack.HuggingFace.Scheduler.SigmaScheduleType, Common.SigmaScheduleType>(),
                    Thresholding = edmDPMSolverMultistep.Thresholding,
                    DynamicThresholdingRatio = edmDPMSolverMultistep.DynamicThresholdingRatio,
                    SampleMaxValue = edmDPMSolverMultistep.SampleMaxValue,
                    SolverOrder = edmDPMSolverMultistep.SolverOrder,
                    SolverType = edmDPMSolverMultistep.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    LowerOrderFinal = edmDPMSolverMultistep.LowerOrderFinal,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.FlowMatchLCMOptions flowMatchLCMOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = flowMatchLCMOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = flowMatchLCMOptions.NumTrainTimesteps,
                    Shift = flowMatchLCMOptions.Shift,
                    BaseShift = flowMatchLCMOptions.BaseShift ?? 0,
                    MaxShift = flowMatchLCMOptions.MaxShift ?? 0,
                    ShiftTerminal = flowMatchLCMOptions.ShiftTerminal,
                    TimeShiftType = flowMatchLCMOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    UpscaleMode = flowMatchLCMOptions.UpscaleMode.Cast<TensorStack.HuggingFace.Scheduler.UpscaleModeType, Common.UpscaleModeType>(),
                    UseDynamicShifting = flowMatchLCMOptions.UseDynamicShifting,
                    InvertSigmas = flowMatchLCMOptions.InvertSigmas,
                    BaseImageSeqLen = flowMatchLCMOptions.BaseImageSeqLen,     // TODO
                    MaxImageSeqLen = flowMatchLCMOptions.MaxImageSeqLen,       // TODO
                    ScaleFactors = flowMatchLCMOptions.ScaleFactors,           // TODO
                    UseKarrasSigmas = flowMatchLCMOptions.UseKarrasSigmas,
                    UseBetaSigmas = flowMatchLCMOptions.UseBetaSigmas,
                    UseExponentialSigmas = flowMatchLCMOptions.UseExponentialSigmas,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.IPNDMOptions ipndmOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = ipndmOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = ipndmOptions.NumTrainTimesteps,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.CogVideoXDDIMOptions cogDDIMOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = cogDDIMOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = cogDDIMOptions.NumTrainTimesteps,
                    BetaEnd = cogDDIMOptions.BetaEnd,
                    BetaStart = cogDDIMOptions.BetaStart,
                    BetaSchedule = cogDDIMOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = cogDDIMOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = cogDDIMOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = cogDDIMOptions.StepsOffset,
                    ClipSample = cogDDIMOptions.ClipSample,
                    ClipSampleRange = cogDDIMOptions.ClipSampleRange,
                    SampleMaxValue = cogDDIMOptions.SampleMaxValue,
                    SetAlphaToOne = cogDDIMOptions.SetAlphaToOne,
                    SNRShiftScale = cogDDIMOptions.SNRShiftScale,
                    RescaleBetasZeroSNR = cogDDIMOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.CogVideoXDPMOptions cogDPMOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = cogDPMOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = cogDPMOptions.NumTrainTimesteps,
                    BetaEnd = cogDPMOptions.BetaEnd,
                    BetaStart = cogDPMOptions.BetaStart,
                    BetaSchedule = cogDPMOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = cogDPMOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = cogDPMOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = cogDPMOptions.StepsOffset,
                    ClipSample = cogDPMOptions.ClipSample,
                    ClipSampleRange = cogDPMOptions.ClipSampleRange,
                    SampleMaxValue = cogDPMOptions.SampleMaxValue,
                    SetAlphaToOne = cogDPMOptions.SetAlphaToOne,
                    SNRShiftScale = cogDPMOptions.SNRShiftScale,
                    RescaleBetasZeroSNR = cogDPMOptions.RescaleBetasZeroSNR,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.HeliosOptions heliosOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = heliosOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = heliosOptions.NumTrainTimesteps,
                    PredictionType = heliosOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    Shift = heliosOptions.Shift,
                    Gamma = heliosOptions.Gamma,
                    SolverOrder = heliosOptions.SolverOrder,
                    SolverType = heliosOptions.SolverType.Cast<TensorStack.HuggingFace.Scheduler.SolverType, Common.SolverType>(),
                    LowerOrderFinal = heliosOptions.LowerOrderFinal,
                    TimeShiftType = heliosOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    UseDynamicShifting = heliosOptions.UseDynamicShifting,
                    UseFlowSigmas = heliosOptions.UseFlowSigmas,
                    PredictX0 = heliosOptions.PredictX0,
                    Thresholding = heliosOptions.Thresholding,
                    Stages = heliosOptions.Stages,                            // TODO
                    StageRange = heliosOptions.StageRange,                    // TODO
                    DisableCorrector = heliosOptions.DisableCorrector,        // TODO
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.HeliosDMDOptions heliosDMDOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = heliosDMDOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = heliosDMDOptions.NumTrainTimesteps,
                    PredictionType = heliosDMDOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    Shift = heliosDMDOptions.Shift,
                    Gamma = heliosDMDOptions.Gamma,
                    TimeShiftType = heliosDMDOptions.TimeShiftType.Cast<TensorStack.HuggingFace.Scheduler.TimeShiftType, Common.TimeShiftType>(),
                    UseDynamicShifting = heliosDMDOptions.UseDynamicShifting,
                    UseFlowSigmas = heliosDMDOptions.UseFlowSigmas,
                    Stages = heliosDMDOptions.Stages,                            // TODO
                    StageRange = heliosDMDOptions.StageRange,                    // TODO
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.TCDOptions tcdOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = tcdOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = tcdOptions.NumTrainTimesteps,
                    BetaEnd = tcdOptions.BetaEnd,
                    BetaStart = tcdOptions.BetaStart,
                    BetaSchedule = tcdOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = tcdOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = tcdOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = tcdOptions.StepsOffset,
                    Thresholding = tcdOptions.Thresholding,
                    DynamicThresholdingRatio = tcdOptions.DynamicThresholdingRatio,
                    TimestepScaling = tcdOptions.TimestepScaling,
                    ClipSample = tcdOptions.ClipSample,
                    ClipSampleRange = tcdOptions.ClipSampleRange,
                    SampleMaxValue = tcdOptions.SampleMaxValue,
                    OriginalInferenceSteps = tcdOptions.OriginalInferenceSteps,
                    SetAlphaToOne = tcdOptions.SetAlphaToOne,
                    RescaleBetasZeroSNR = tcdOptions.RescaleBetasZeroSNR, // TODO
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.SCMOptions scmOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = scmOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = scmOptions.NumTrainTimesteps,
                    PredictionType = scmOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    SigmaData = scmOptions.SigmaData,
                };
            }
            else if (options is TensorStack.HuggingFace.Scheduler.SASolverOptions saSolverOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = saSolverOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = saSolverOptions.NumTrainTimesteps,
                    BetaEnd = saSolverOptions.BetaEnd,
                    BetaStart = saSolverOptions.BetaStart,
                    BetaSchedule = saSolverOptions.BetaSchedule.Cast<TensorStack.HuggingFace.Scheduler.BetaScheduleType, Common.BetaScheduleType>(),
                    PredictionType = saSolverOptions.PredictionType.Cast<TensorStack.HuggingFace.Scheduler.PredictionType, Common.PredictionType>(),
                    TimestepSpacing = saSolverOptions.TimestepSpacing.Cast<TensorStack.HuggingFace.Scheduler.TimestepSpacingType, Common.TimestepSpacingType>(),
                    StepsOffset = saSolverOptions.StepsOffset,
                    Thresholding = saSolverOptions.Thresholding,
                    DynamicThresholdingRatio = saSolverOptions.DynamicThresholdingRatio,
                    SampleMaxValue = saSolverOptions.SampleMaxValue,
                    FlowShift = saSolverOptions.FlowShift,
                    AlgorithmType = saSolverOptions.AlgorithmType.Cast<TensorStack.HuggingFace.Scheduler.AlgorithmType, Common.AlgorithmType>(),
                    VarianceType = saSolverOptions.VarianceType.Cast<TensorStack.HuggingFace.Scheduler.VarianceType, Common.VarianceType>(),
                    UseFlowSigmas = saSolverOptions.UseFlowSigmas,
                    LowerOrderFinal = saSolverOptions.LowerOrderFinal,
                    PredictorOrder = saSolverOptions.PredictorOrder,
                    CorrectorOrder = saSolverOptions.CorrectorOrder,
                    UseBetaSigmas = saSolverOptions.UseBetaSigmas,
                    UseExponentialSigmas = saSolverOptions.UseExponentialSigmas,
                    UseKarrasSigmas = saSolverOptions.UseKarrasSigmas,
                };
            }
            else if (options is LTXEulerAncestralRFOptions ltxEulerAncestralRFOptions)
            {
                return new Common.SchedulerOptions
                {
                    Scheduler = ltxEulerAncestralRFOptions.Scheduler.Cast<TensorStack.HuggingFace.Scheduler.SchedulerType, Common.SchedulerType>(),
                    NumTrainTimesteps = ltxEulerAncestralRFOptions.NumTrainTimesteps,
                    Eta = ltxEulerAncestralRFOptions.Eta,
                    SNoise = ltxEulerAncestralRFOptions.SNoise,
                };
            }

            throw new NotImplementedException();
        }


        public static TensorStack.HuggingFace.Common.ConversationMessage[] ToPythonOptions(this Common.ConversationMessage[] messages)
        {
            return messages.Select(x => new TensorStack.HuggingFace.Common.ConversationMessage(x.Role.Cast<Common.ConversationRole, TensorStack.HuggingFace.Common.ConversationRole>(), x.Content, x.ImageIndex, x.AudioIndex)).ToArray();
        }


        private static TensorStack.HuggingFace.Scheduler.SigmaScheduleType AsPythonType(this Common.SigmaScheduleType sigmaSchedule)
        {
            return sigmaSchedule switch
            {
                Common.SigmaScheduleType.Exponential => TensorStack.HuggingFace.Scheduler.SigmaScheduleType.Exponential,
                _ => TensorStack.HuggingFace.Scheduler.SigmaScheduleType.Karras
            };
        }


        private static PipelineType GetPipelineType(this PipelineLoadOptions options)
        {
            if (options.Pipeline == PipelineType.Qwen3Pipeline || options.Pipeline == PipelineType.Gemma4Pipeline)
                return PipelineType.AutoTextPipeline;

            return options.Pipeline;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U Cast<T, U>(this T source)
            where T : struct, Enum
            where U : struct, Enum
        {
            return Unsafe.As<T, U>(ref source);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U Cast<T, U>(this T? source)
            where T : struct, Enum
            where U : struct, Enum
        {
            if (source == null)
                return default;

            return source.Value.Cast<T, U>();
        }
    }
}
