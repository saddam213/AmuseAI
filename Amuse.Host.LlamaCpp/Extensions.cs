using Amuse.Common;
using System;
using System.Linq;
using TensorStack.LlamaCpp;
using PipelineCommon = TensorStack.LlamaCpp.Common;

namespace Amuse.Host.LlamaCpp
{
    internal static class Extensions
    {
        private const int DefaultContextSize = 16384;

        /// <summary>
        /// Creates the model load options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="pipelineOptions">The pipeline options.</param>
        /// <returns>PipelineCommon.LlamaCppOptions.</returns>
        internal static PipelineCommon.LlamaCppOptions CreateLoadOptions(this PipelineCreateOptions options, PipelineLoadOptions pipelineOptions)
        {
            var backend = GetBackend(options);
            return new PipelineCommon.LlamaCppOptions
            {
                ModelPath = GetModelPath(pipelineOptions),
                ProjectorPath = GetProjectorPath(pipelineOptions),
                Template = GetTemplate(pipelineOptions),
                Device = GetDevice(pipelineOptions, backend),
                GpuLayers = GetGpuLayers(pipelineOptions),
                Threads = pipelineOptions.ExecutionOptions?.Threads ?? 0,
                IsCacheOffloadEnabled = GetCacheOffload(pipelineOptions),
                ContextSize = pipelineOptions.ExecutionOptions?.ContextSize ?? DefaultContextSize,
                IsFlashAttentionEnabled = GetFlashAttention(pipelineOptions.ExecutionOptions)
            };
        }


        /// <summary>
        /// Creates the GenerateTextOptions.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>PipelineCommon.GenerateTextOptions.</returns>
        internal static PipelineCommon.GenerateTextOptions CreateTextOptions(this GenerateTextOptions options)
        {
            return new PipelineCommon.GenerateTextOptions
            {
                Seed = options.Seed,
                Prompt = options.Prompt,
                Conversation = options.Conversation?.Select(x => x.CreateMessage()).ToList(),
                MaxLength = options.MaxLength,
                IsSamplingEnabled = options.IsSamplingEnabled,
                Temperature = options.Temperature,
                TopK = options.TopK,
                TopP = options.TopP,
                TypicalP = options.TypicalP > 0 ? options.TypicalP : 1f,
                RepetitionPenalty = options.RepetitionPenalty > 0 ? options.RepetitionPenalty : 1f,
                IsThinkingEnabled = options.IsThinkingEnabled,
                Tools = options.Tools,
                InputImages = options.InputImages,
                InputAudios = options.InputAudios
            };
        }


        /// <summary>
        /// Creates the conversation message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>PipelineCommon.ConversationMessage.</returns>
        private static PipelineCommon.ConversationMessage CreateMessage(this ConversationMessage message)
        {
            return new PipelineCommon.ConversationMessage((TensorStack.LlamaCpp.ConversationRole)(int)message.Role, message.Content, message.ImageIndex, message.AudioIndex, message.ToolCalls);
        }


        /// <summary>
        /// Gets the llama.cpp backend name.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="System.Exception">Backend Not Found</exception>
        private static string GetBackend(PipelineCreateOptions options)
        {
            var backend = options.HostVersion?.ToLowerInvariant();
            return backend switch
            {
                "cuda" or "vulkan" => backend,
                "rocm" or "hip" => "rocm",
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
            return backend == "vulkan" ? pipelineOptions.DeviceId : pipelineOptions.DeviceVendorIndex;
        }


        /// <summary>
        /// Gets the model path, a single GGUF file.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetModelPath(PipelineLoadOptions pipelineOptions)
        {
            var modelPath = pipelineOptions.CheckpointConfig?.TextEncoder;
            if (string.IsNullOrEmpty(modelPath))
                throw new Exception("Checkpoint does not contain a model path (TextModel).");

            return modelPath;
        }


        /// <summary>
        /// Gets the multimodal projector path (mmproj GGUF), null for text only models.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static string GetProjectorPath(PipelineLoadOptions pipelineOptions)
        {
            var projectorPath = pipelineOptions.CheckpointConfig?.TextEncoder2;
            return string.IsNullOrEmpty(projectorPath) ? null : projectorPath;
        }


        /// <summary>
        /// Gets the prompt template from the model template name (e.g. Qwen35_IT), falling back to the pipeline type.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static PromptTemplate GetTemplate(PipelineLoadOptions pipelineOptions)
        {
            var template = pipelineOptions.Template ?? string.Empty;
            if (template.StartsWith("Qwen30", StringComparison.OrdinalIgnoreCase))
                return PromptTemplate.Qwen30;
            if (template.StartsWith("Qwen35", StringComparison.OrdinalIgnoreCase))
                return PromptTemplate.Qwen35;
            if (template.StartsWith("Qwen36", StringComparison.OrdinalIgnoreCase))
                return PromptTemplate.Qwen36;
            if (template.StartsWith("Qwen38", StringComparison.OrdinalIgnoreCase))
                return PromptTemplate.Qwen38;
            if (template.StartsWith("Gemma4", StringComparison.OrdinalIgnoreCase))
                return PromptTemplate.Gemma4;

            return pipelineOptions.Pipeline switch
            {
                PipelineType.Gemma4Pipeline => PromptTemplate.Gemma4,
                PipelineType.Qwen3Pipeline => PromptTemplate.Qwen35,
                _ => throw new NotSupportedException($"Template '{template}' is not supported by llama.cpp.")
            };
        }


        /// <summary>
        /// Gets the number of layers to place on the GPU for the memory mode.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static int GetGpuLayers(PipelineLoadOptions pipelineOptions)
        {
            return pipelineOptions.MemoryMode switch
            {
                MemoryModeType.Device => -1,
                MemoryModeType.OffloadModel => -1,
                MemoryModeType.OffloadCPU => 0,
                _ => pipelineOptions.ExecutionOptions?.GpuLayers ?? -1
            };
        }


        /// <summary>
        /// Gets whether the KV cache is kept on the GPU for the memory mode.
        /// </summary>
        /// <param name="pipelineOptions">The pipeline options.</param>
        private static bool GetCacheOffload(PipelineLoadOptions pipelineOptions)
        {
            return pipelineOptions.MemoryMode switch
            {
                MemoryModeType.Device => true,
                MemoryModeType.OffloadModel => false,
                MemoryModeType.OffloadCPU => false,
                _ => pipelineOptions.ExecutionOptions?.IsCacheOffloadEnabled ?? true
            };
        }


        /// <summary>
        /// Gets the flash attention setting, null lets llama.cpp decide.
        /// </summary>
        /// <param name="executionOptions">The execution options.</param>
        private static bool? GetFlashAttention(ExecutionOptions executionOptions)
        {
            return executionOptions?.TextEncoderFlashAttention switch
            {
                AutoOption.Enabled => true,
                AutoOption.Disabled => false,
                _ => null
            };
        }


        /// <summary>
        /// Translates the data type to the llama.cpp KV cache type, unsupported types fall back to F16.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        internal static CacheDataType ToCacheDataType(this TensorStack.Common.DataType dataType)
        {
            return dataType switch
            {
                TensorStack.Common.DataType.Q8_0 => CacheDataType.Q8_0,
                TensorStack.Common.DataType.Q4_0 => CacheDataType.Q4_0,
                _ => CacheDataType.F16
            };
        }
    }
}
