using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TensorStack.Common;
using TensorStack.Python.Common;
using TensorStack.Python.Config;

namespace Amuse.App
{
    public static class Extensions
    {

        /// <summary>
        /// Gets the file extesnion.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        public static string GetExtension(this MediaType mediaType)
        {
            return mediaType switch
            {
                MediaType.Text => "txt",
                MediaType.Audio => "wav",
                MediaType.Video => "mp4",
                MediaType.Image => "png",
                _ => throw new NotSupportedException()
            };
        }


        public static int GetIndex(this MemoryProfile profile, int deviceMemory)
        {
            int bestIndex = -1;
            int bestValue = int.MinValue;

            for (int i = 0; i < profile.MemoryModes.Length; i++)
            {
                int value = profile.MemoryModes[i];
                if (value <= deviceMemory && value > bestValue)
                {
                    bestValue = value;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                bestIndex = 0;

            return bestIndex;
        }



        public static bool HasChanged(this IReadOnlyList<LoraAdapterModel> existingAdapters, IReadOnlyList<LoraAdapterModel> newAdapters)
        {
            if (ReferenceEquals(existingAdapters, newAdapters))
                return false;

            if (existingAdapters == null || newAdapters == null)
                return true;

            if (existingAdapters.Count != newAdapters.Count)
                return true;

            for (int i = 0; i < existingAdapters.Count; i++)
            {
                if (!string.Equals(existingAdapters[i]?.Key, newAdapters[i]?.Key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }



        public static CheckpointConfig ToConfig(this DiffusionCheckpointModel diffusionCheckpoint)
        {
            if (diffusionCheckpoint is null)
                return null;

            if (!string.IsNullOrEmpty(diffusionCheckpoint.SingleFile))
            {
                return new CheckpointConfig
                {
                    SingleFile = diffusionCheckpoint.SingleFile
                };
            }

            return new CheckpointConfig
            {
                TextEncoder = diffusionCheckpoint.TextEncoder,
                TextEncoder2 = diffusionCheckpoint.TextEncoder2,
                TextEncoder3 = diffusionCheckpoint.TextEncoder3,
                Transformer = diffusionCheckpoint.Transformer,
                Transformer2 = diffusionCheckpoint.Transformer2,
                Vae = diffusionCheckpoint.Vae,
                AudioVae = diffusionCheckpoint.AudioVae,
                Vocoder = diffusionCheckpoint.Vocoder,
                Connectors = diffusionCheckpoint.Connectors
            };
        }



        public static List<LoraConfig> GetLoraAdapters(this LoraAdapterModel[] loraAdapterModel)
        {
            if (loraAdapterModel.IsNullOrEmpty())
                return default;

            return [.. loraAdapterModel.Select(lora => new LoraConfig
            {
                Path = lora.Path,
                Weights = lora.Weights,
                Name = lora.Key,
                IsOfflineMode = lora.Status == ModelStatusType.Installed
            })];
        }


        public static List<LoraOptions> GetLoraOptions(this DiffusionInputOptions options)
        {
            if (options.LoraOptions.IsNullOrEmpty())
                return default;

            return [.. options.LoraOptions.Select(x => new LoraOptions
            {
                Name = x.Key,
                Strength = x.Strength
            })];
        }


        public static ControlNetConfig GetControlNet(this ControlNetModel controlNetModel)
        {
            if (controlNetModel is null)
                return null;

            return new ControlNetConfig
            {
                Name = controlNetModel.Name,
                Path = controlNetModel.Path,
                IsOfflineMode = controlNetModel.Status == ModelStatusType.Installed
            };
        }



        public static bool AddIfNotNull<TSource>(this IList<TSource> source, TSource item)
        {
            if (item is null)
                return false;

            source.Add(item);
            return true;
        }

    }

    public static partial class Utils
    {
        public const int FixedIdRange = 1000;
    }



    public static class FontOptions
    {
        public static FontWeight[] FontWeightList { get; } = new[]
           {
            FontWeights.Thin,
            FontWeights.ExtraLight,
            FontWeights.Light,
            FontWeights.Normal,
            FontWeights.Medium,
            FontWeights.SemiBold,
            FontWeights.Bold,
            FontWeights.ExtraBold,
            FontWeights.Black
        };


        public static FontStyle[] FontStyleList { get; } = new[]
        {
            FontStyles.Normal,
            FontStyles.Italic,
            FontStyles.Oblique
        };


        public static ICollection<FontFamily> FontFamilies { get; } = System.Windows.Media.Fonts.SystemFontFamilies;
    }


    public static class BrushOptions
    {
        public static IEnumerable<Brush> AllBrushes { get; } =
            typeof(Brushes).GetProperties()
                .Where(p => p.PropertyType == typeof(Brush))
                .Select(p => (Brush)p.GetValue(null));
    }
}
