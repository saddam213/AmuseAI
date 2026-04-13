using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using TensorStack.Python.Config;

namespace Amuse.App
{
    public static class Extensions
    {

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

    }

    public static partial class Utils
    {
        public const int FixedIdRange = 1000;

        public static string GetHuggingFaceCacheId(string repositoryUrl)
        {
            return $"models--{repositoryUrl.Replace("/", "--")}";
        }

        public static bool IsCheckpointInstalled(string modelDirectory, string checkpoint)
        {
            if (string.IsNullOrEmpty(checkpoint))
                return false;

            if (File.Exists(checkpoint))
                return true;

            if (!TryParseHuggingFaceRepo(checkpoint, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetHuggingFaceCacheId(repositoryId));
            if (!Directory.Exists(directory))
                return false;

            var filename = Path.GetFileName(checkpoint);
            return Directory.EnumerateFiles(directory, filename, SearchOption.AllDirectories).Any();
        }


        public static bool IsLoraAdapterInstalled(string modelDirectory, string path, string weights)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(weights))
                return false;

            var adapter = Path.Combine(path, weights);
            if (File.Exists(adapter))
                return true;

            if (!TryParseHuggingFaceRepo(path, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetHuggingFaceCacheId(repositoryId));
            if (!Directory.Exists(directory))
                return false;

            return Directory.EnumerateFiles(directory, weights, SearchOption.AllDirectories).Any();
        }


        public static bool IsControlNetInstalled(string modelDirectory, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (File.Exists(path))
                return true;

            if (!TryParseHuggingFaceRepo(path, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetHuggingFaceCacheId(repositoryId));
            return Directory.Exists(directory);
        }


        public static bool IsHuggingFaceLink(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2)
                return true; // username/repository

            return HuggingFaceLinkRegex.IsMatch(url);
        }


        public static bool TryParseHuggingFaceRepo(string input, out string repoId)
        {
            repoId = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (input.Contains("/blob/main/"))
                input = input.Split("/blob/main/").FirstOrDefault();

            var match = HuggingFaceRepoRegex.Match(input.Trim());
            if (!match.Success)
                return false;

            repoId = match.Groups["repo"].Value;
            return true;
        }

        private static readonly Regex HuggingFaceLinkRegex = CreateHuggingFaceLinkRegex();
        private static readonly Regex HuggingFaceRepoRegex = CreateHuggingFaceRepoRegex();

        [GeneratedRegex(@"^https?:\/\/(www\.)?huggingface\.co\/(datasets\/|spaces\/)?[\w.-]+\/[\w.-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NZ")]
        private static partial Regex CreateHuggingFaceLinkRegex();

        [GeneratedRegex(@"^(?:https?:\/\/)?(?:www\.)?huggingface\.co\/(?<repo>[^\/\s]+\/[^\/\s]+)$|^(?<repo>[^\/\s]+\/[^\/\s]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NZ")]
        private static partial Regex CreateHuggingFaceRepoRegex();
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
