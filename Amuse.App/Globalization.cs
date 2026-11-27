using Amuse.App.Resources;
using Amuse.Common;
using System.Collections.Generic;

namespace Amuse.App
{
    public static class Globalization
    {
        public static string GetProgressMessage(PipelineProgress progress)
        {
            if (!string.IsNullOrEmpty(progress.Subkey) && ProgressMessages.TryGetValue(progress.Subkey, out string resourceKey))
            {
                return string.Format(
                    AppDefault.ResourceManager.GetString(resourceKey, AppDefault.Culture),
                    progress.Key,
                    progress.Subkey,
                    progress.ElapsedKey,
                    progress.Timestamp,
                    progress.Elapsed,
                    progress.Value,
                    progress.Maximum,
                    progress.BatchValue,
                    progress.BatchMaximum,
                    progress.Message
                );
            }
            return progress.Message ?? progress.Subkey ?? progress.Key;
        }


        private readonly static Dictionary<string, string> ProgressMessages = new Dictionary<string, string>()
        {
            {"Initialize",  nameof(AppDefault.ProgressInitialize) },
            {"TextEncoder", nameof(AppDefault.ProgressTextEncoder) },
            {"Transformer", nameof(AppDefault.ProgressTransformer) },
            {"Step",        nameof(AppDefault.ProgressStep) },
            {"Encoder",     nameof(AppDefault.ProgressEncoder) },
            {"AutoEncoder", nameof(AppDefault.ProgressAutoEncoder) },
            {"Complete",    nameof(AppDefault.ProgressComplete) },
            {"Upscaling",   nameof(AppDefault.ProgressUpscaling) },
            {"Chunk",       nameof(AppDefault.ProgressChunk) }
        };
    }
}
