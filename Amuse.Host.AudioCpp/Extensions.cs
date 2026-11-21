using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TensorStack.Common;
using PipelineCommon = TensorStack.AudioCpp.Common;

namespace Amuse.Host.AudioCpp
{
    internal static class Extensions
    {
        /// <summary>
        /// Creates the model load options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.AudioCppOptions.</returns>
        internal static PipelineCommon.AudioCppOptions CreateLoadOptions(this PipelineCreateOptions options, PipelineLoadOptions pipelineOptions)
        {
            var backend = GetBackend(options);
            return new PipelineCommon.AudioCppOptions
            {
                ModelPath = GetModelPath(pipelineOptions),
                Pipeline = GetPipeline(pipelineOptions.Pipeline),
                Backend = backend,
                Device = GetDevice(pipelineOptions, backend),
                Threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 16),
                IsMemorySaverEnabled = pipelineOptions.MemoryMode != MemoryModeType.Device,
                DataType = (pipelineOptions.ExecutionOptions?.WeightType ?? DataType.Default).ToNativeDataType()
            };
        }


        /// <summary>
        /// Creates the GenerateAudioOptions (music, sound effects, speech).
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>PipelineCommon.GenerateAudioOptions.</returns>
        internal static PipelineCommon.GenerateAudioOptions CreateAudioOptions(this GenerateAudioOptions options, PipelineLoadOptions pipelineOptions, ILogger logger)
        {
            return new PipelineCommon.GenerateAudioOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Prompt2 = options.Prompt2,
                NegativePrompt = options.NegativePrompt,
                GuidanceScale = options.GuidanceScale,
                GuidanceScale2 = options.GuidanceScale2,
                Steps = options.Steps,
                Strength = options.Strength,
                Duration = options.Duration,
                Language = GetLanguage(options.Language),
                Instruction = options.Instruction,
                Task = options.Task,
                Bpm = options.Bpm,
                Keyscale = options.Keyscale,
                TrackName = options.TrackName,
                TimeSignature = options.TimeSignature,
                Speed = options.Speed,
                InputAudios = options.InputAudios,
                SessionOptions = GetLoraSessionOptions(pipelineOptions?.LoraAdapters, options.LoraOptions, logger)
            };
        }


        /// <summary>
        /// Gets the LoRA session options (lora, lora_scale), audio.cpp applies a single adapter when the session loads,
        /// so the pipeline recreates the session when the adapter or strength changes between requests.
        /// </summary>
        /// <param name="loraAdapters">The lora adapters.</param>
        /// <param name="loraAdapterOptions">The lora adapter options.</param>
        /// <param name="logger">The logger.</param>
        private static Dictionary<string, string> GetLoraSessionOptions(List<LoraConfig> loraAdapters, List<LoraOptions> loraAdapterOptions, ILogger logger)
        {
            var sessionOptions = new Dictionary<string, string>();
            if (loraAdapters.IsNullOrEmpty() || loraAdapterOptions.IsNullOrEmpty())
                return sessionOptions;

            // Adapter
            var loraAdapter = loraAdapters.FirstOrDefault(x => loraAdapterOptions.Any(o => o.Name == x.Name));
            if (loraAdapter == null)
                return sessionOptions;

            if (loraAdapters.Count > 1)
                logger?.LogWarning("[AmuseHost] [Extensions] [GetLoraSessionOptions] audio.cpp supports a single LoRA adapter, using '{Name}'", loraAdapter.Name);

            // PEFT adapters load from their directory (adapter_config.json + adapter_model.safetensors), others from the weights file
            var loraPath = Path.Combine(loraAdapter.Path, loraAdapter.Weights ?? string.Empty);
            if (File.Exists(Path.Combine(loraAdapter.Path, "adapter_config.json")))
                loraPath = loraAdapter.Path;

            // Strength
            var strength = loraAdapterOptions.First(x => x.Name == loraAdapter.Name).Strength;
            sessionOptions["lora"] = loraPath;
            sessionOptions["lora_scale"] = strength.ToString(CultureInfo.InvariantCulture);
            return sessionOptions;
        }


        /// <summary>
        /// Creates the GenerateTextOptions (speech recognition).
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>PipelineCommon.GenerateTextOptions.</returns>
        internal static PipelineCommon.GenerateTextOptions CreateTextOptions(this GenerateTextOptions options)
        {
            return new PipelineCommon.GenerateTextOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Language = GetLanguage(options.Language),
                MaxLength = options.MaxLength,
                IsSamplingEnabled = options.IsSamplingEnabled,
                Beams = options.Beams,
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                RepetitionPenalty = options.RepetitionPenalty,
                ChunkSize = options.ChunkSize,
                InputAudios = options.InputAudios
            };
        }


        /// <summary>
        /// Gets the audio.cpp backend name.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="System.Exception">Backend Not Found</exception>
        private static string GetBackend(PipelineCreateOptions options)
        {
            var backend = options.HostVersion?.ToLowerInvariant();
            return backend switch
            {
                "cpu" or "cuda" or "vulkan" => backend,
                "rocm" or "hip" => "hip",
                _ => throw new Exception($"{options.HostVersion} Backend Not Found.")
            };
        }


        /// <summary>
        /// Gets the backend device index.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <param name="backend">The backend.</param>
        private static int GetDevice(PipelineLoadOptions pipelineOptions, string backend)
        {
            return backend switch
            {
                "cpu" => 0,
                "vulkan" => pipelineOptions.DeviceId,
                _ => pipelineOptions.DeviceVendorIndex
            };
        }


        /// <summary>
        /// Gets the model path, a single GGUF file or a model directory.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetModelPath(PipelineLoadOptions pipelineOptions)
        {
            var checkpoint = pipelineOptions.CheckpointConfig;
            var modelPath = checkpoint?.FullCheckpoint ?? checkpoint?.Transformer ?? checkpoint?.Unet;
            if (string.IsNullOrEmpty(modelPath))
                throw new Exception("Checkpoint does not contain a model path (Transformer).");

            return modelPath;
        }


        /// <summary>
        /// Gets the audio.cpp pipeline, null lets audio.cpp identify the model from its path.
        /// </summary>
        /// <param name="pipelineType">Type of the pipeline.</param>
        private static string GetPipeline(PipelineType pipelineType)
        {
            return pipelineType switch
            {
                PipelineType.AceStepPipeline => "ace_step",
                PipelineType.CanaryPipeline => "canary_asr",
                PipelineType.ChatterboxPipeline => "chatterbox_turbo",
                PipelineType.CohereASRPipeline => "cohere_asr",
                PipelineType.MiniMaxAudioPipeline => "minimax_music3",
                PipelineType.MossSoundEffectPipeline => "moss_soundeffect",
                PipelineType.OmniVoicePipeline => "omnivoice",
                PipelineType.ParakeetPipeline => "parakeet_tdt",
                PipelineType.Qwen3ASRPipeline => "qwen3_asr",
                PipelineType.Qwen3TTSPipeline => "qwen3_tts",
                PipelineType.StableAudioPipeline => "stable_audio",
                PipelineType.SupertonicPipeline => "supertonic",
                PipelineType.VoxtralTTSPipeline => "voxtral_tts",
                PipelineType.YuEPipeline => "yue2",
                _ => default
            };
        }


        /// <summary>
        /// Gets the language code, null to use the model default.
        /// </summary>
        /// <param name="language">The language.</param>
        private static string GetLanguage(LanguageType language)
        {
            if (language == LanguageType.None || language == LanguageType.Auto)
                return default;

            return language.GetShortName();
        }


        /// <summary>
        /// Translates the data type to the audio.cpp weight type, unsupported types fall back to the model default.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        internal static TensorStack.AudioCpp.DataType ToNativeDataType(this DataType dataType)
        {
            return dataType switch
            {
                DataType.Float32 => TensorStack.AudioCpp.DataType.F32,
                DataType.Float16 => TensorStack.AudioCpp.DataType.F16,
                DataType.BFloat16 => TensorStack.AudioCpp.DataType.BF16,
                DataType.Q8_0 => TensorStack.AudioCpp.DataType.Q8_0,
                DataType.Q4_0 => TensorStack.AudioCpp.DataType.Q4_0,
                DataType.Q4_K => TensorStack.AudioCpp.DataType.Q4_K,
                _ => TensorStack.AudioCpp.DataType.Default
            };
        }
    }
}
