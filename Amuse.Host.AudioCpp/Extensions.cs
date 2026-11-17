using Amuse.Common;
using System;
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
                IsMemorySaverEnabled = pipelineOptions.MemoryMode != MemoryModeType.Device
            };
        }


        /// <summary>
        /// Creates the GenerateAudioOptions (music, sound effects, speech).
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>PipelineCommon.GenerateAudioOptions.</returns>
        internal static PipelineCommon.GenerateAudioOptions CreateAudioOptions(this GenerateAudioOptions options)
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
                InputAudios = options.InputAudios
            };
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
                PipelineType.MiniMaxAudioPipeline => "minimax_music3",
                PipelineType.ParakeetPipeline => "parakeet_tdt",
                PipelineType.StableAudioPipeline => "stable_audio",
                PipelineType.SupertonicPipeline => "supertonic",
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
    }
}
