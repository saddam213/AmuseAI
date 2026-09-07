using Amuse.Common;
using System;

namespace Amuse.Host.OnnxRuntime
{
    public static class Extensions
    {
        public static TensorStack.OnnxRuntime.LLM.Pipelines.Whisper.LanguageType GetLanguageType(this Common.GenerateTextOptions options)
        {
            if (Enum.TryParse<TensorStack.OnnxRuntime.LLM.Pipelines.Whisper.LanguageType>(options.Language.GetShortName(), true, out var languageType))
                return languageType;

            return TensorStack.OnnxRuntime.LLM.Pipelines.Whisper.LanguageType.EN;
        }
    }
}
