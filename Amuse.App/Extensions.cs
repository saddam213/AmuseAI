using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Image;

namespace Amuse.App
{
    public static class Extensions
    {
        private static readonly SearchValues<char> InvalidPathChars = SearchValues.Create(Path.GetInvalidPathChars());

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


        /// <summary>
        /// Resolves the Auto memory mode from the model memory size and the device memory.
        /// </summary>
        /// <param name="memorySize">The model memory size in GB, 0 if unknown.</param>
        /// <param name="deviceMemory">The device memory in GB.</param>
        public static MemoryMode ResolveMemoryMode(float memorySize, int deviceMemory)
        {
            if (memorySize <= 0 || memorySize <= deviceMemory)
                return MemoryMode.High;
            if (memorySize * 0.6f <= deviceMemory)
                return MemoryMode.Medium;

            return MemoryMode.Low;
        }


        /// <summary>
        /// Gets the data types the backend can load model weights from.
        /// </summary>
        /// <param name="backend">The backend.</param>
        public static DataType[] GetDataTypes(this BackendType backend)
        {
            return backend switch
            {
                BackendType.HuggingFace =>
                [
                    DataType.BFloat16, DataType.Float16, DataType.Float32, DataType.Float8
                ],
                BackendType.StableDiffusionCpp =>
                [
                    DataType.BFloat16, DataType.Float16, DataType.Float32, DataType.Float8,
                    DataType.Q8_0, DataType.Q6_K, DataType.Q5_K, DataType.Q5_1, DataType.Q5_0, DataType.Q4_K, DataType.Q4_1, DataType.Q4_0, DataType.Q3_K, DataType.Q2_K
                ],
                BackendType.AudioCpp =>
                [
                    DataType.BFloat16, DataType.Float16, DataType.Float32,
                    DataType.Q8_0, DataType.Q4_K, DataType.Q4_0
                ],
                BackendType.LlamaCpp =>
                [
                    DataType.BFloat16, DataType.Float16, DataType.Float32,
                    DataType.Q8_0, DataType.Q6_K, DataType.Q5_K, DataType.Q5_0, DataType.Q4_K, DataType.Q4_0, DataType.Q3_K, DataType.Q2_K,
                    DataType.IQ4_XS, DataType.IQ4_NL, DataType.MXFP4
                ],
                BackendType.OnnxRuntime =>
                [
                    DataType.Float16, DataType.Float32, DataType.Int8
                ],
                _ => []
            };
        }


        /// <summary>
        /// Gets the weight types the backend can convert model weights to when loading, empty if it cannot.
        /// </summary>
        /// <param name="backend">The backend.</param>
        public static DataType[] GetWeightTypes(this BackendType backend)
        {
            return backend switch
            {
                BackendType.HuggingFace =>
                [
                    DataType.Default, DataType.BFloat16, DataType.Float16, DataType.Float32, DataType.Int8, DataType.Int4
                ],
                BackendType.StableDiffusionCpp =>
                [
                    DataType.Default, DataType.BFloat16, DataType.Float16, DataType.Float32,
                    DataType.Q8_0, DataType.Q6_K, DataType.Q5_K, DataType.Q5_0, DataType.Q4_K, DataType.Q4_0, DataType.Q3_K, DataType.Q2_K
                ],
                BackendType.AudioCpp =>
                [
                    DataType.Default, DataType.BFloat16, DataType.Float16, DataType.Float32,
                    DataType.Q8_0, DataType.Q4_K, DataType.Q4_0
                ],
                _ => []
            };
        }


        /// <summary>
        /// Gets the description of what the memory mode does on the backend, falling back to the generic description.
        /// </summary>
        /// <param name="memoryMode">The memory mode.</param>
        /// <param name="backend">The backend, null if no model is selected.</param>
        public static string GetDescription(this MemoryMode memoryMode, BackendType? backend)
        {
            if (backend is null || memoryMode == MemoryMode.Auto)
                return memoryMode.GetDescription();

            return AppToolTips.ResourceManager.GetString($"ToolTip_MemoryMode_{backend}_{memoryMode}", AppToolTips.Culture)
                ?? AppToolTips.ResourceManager.GetString($"ToolTip_MemoryMode_{backend}", AppToolTips.Culture)
                ?? memoryMode.GetDescription();
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



        public static SchedulerInputOptions[] Copy(this SchedulerInputOptions[] collection)
        {
            if (collection.IsNullOrEmpty())
                return null;

            return collection.Select(x => x with
            {
                ScaleFactors = x.ScaleFactors?.ToList(),
                StageRange = x.StageRange?.ToList(),
                DisableCorrector = x.DisableCorrector?.ToList(),
            }).ToArray();
        }


        public static SizeOption[] Copy(this SizeOption[] collection)
        {
            if (collection.IsNullOrEmpty())
                return [];

            return collection.Select(x => new SizeOption
            {
                Height = x.Height,
                Width = x.Width,
                IsDefault = x.IsDefault
            }).ToArray();
        }


        /// <summary>
        /// Determines whether the model can run on the device, the device supports the model backend and vendor.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="device">The device.</param>
        public static bool IsCompatible(this IGenerateModel model, DeviceModel device)
        {
            if (model is null || device is null)
                return false;

            if (!device.SupportedBackends.Contains(model.Backend))
                return false;

            if (!model.Vendor.IsNullOrEmpty() && !model.Vendor.Contains(device.Vendor))
                return false;

            return true;
        }


        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.AsSpan().IndexOfAny(InvalidPathChars) == -1;
        }


        public static bool AddIfNotNull<TSource>(this IList<TSource> source, TSource item)
        {
            if (item is null)
                return false;

            source.Add(item);
            return true;
        }


        public static int RemoveAll<T>(this IList<T> collection, Predicate<T> condition)
        {
            var removed = 0;
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (condition(collection[i]))
                {
                    collection.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }


        public static int NextId<T>(this Collection<T> collection) where T : IDownloadModel
        {
            return collection.NextId(x => x.Id);
        }


        public static int NextId<T>(this Collection<T> collection, Func<T, int> selector)
        {
            var nextId = Utils.FixedIdRange + 1;
            if (collection.IsNullOrEmpty())
                return nextId;

            return Math.Max(nextId, collection.Max(selector) + 1);
        }


        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                    yield return t;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

    }

    public static partial class Utils
    {
        public const int FixedIdRange = 1000;

        /// <summary>
        /// Estimates the memory size in GB of a model from its parameter count, assuming an 8-bit quant.
        /// </summary>
        /// <param name="parameterCount">The parameter count in billions.</param>
        public static float GetMemorySize(double parameterCount)
        {
            const double size8Bit = 1.0;
            const double overhead8Bit = 1.1;
            return Estimate(parameterCount, size8Bit, overhead8Bit);
        }


        private static int Estimate(double parametersBillion, double bytesPerParam, double overhead)
        {
            return (int)Math.Ceiling(parametersBillion * bytesPerParam * overhead);
        }


        public static int[] GetIndexValues(this Dictionary<int, string> valuePairs)
        {
            if (valuePairs.IsNullOrEmpty())
                return [];

            return [.. valuePairs.Keys.Where(x => x >= 0)];
        }


        public static Dictionary<int, string> GetIndexedInputs(this List<ImageInput> images, int maxCount = 0)
        {
            if (images.IsNullOrEmpty())
                return default;

            var start = maxCount > 0 && images.Count > maxCount ? maxCount - images.Count : 0;
            var dictionary = new Dictionary<int, string>();
            for (var i = 0; i < images.Count; i++)
            {
                dictionary.Add(start + i, images[i].SourceFile);
            }
            return dictionary;
        }


        public static Dictionary<int, string> GetIndexedInputs(this List<AudioInputStream> audioStreams, int maxCount = 0)
        {
            if (audioStreams.IsNullOrEmpty())
                return default;

            var start = maxCount > 0 && audioStreams.Count > maxCount ? maxCount - audioStreams.Count : 0;
            var dictionary = new Dictionary<int, string>();
            for (var i = 0; i < audioStreams.Count; i++)
            {
                dictionary.Add(start + i, audioStreams[i].SourceFile);
            }
            return dictionary;
        }


        public static Dictionary<int, string> GetIndexedInputs(this List<VideoInputStream> videoStreams, int maxCount = 0)
        {
            if (videoStreams.IsNullOrEmpty())
                return default;

            var start = maxCount > 0 && videoStreams.Count > maxCount ? maxCount - videoStreams.Count : 0;
            var dictionary = new Dictionary<int, string>();
            for (var i = 0; i < videoStreams.Count; i++)
            {
                dictionary.Add(start + i, videoStreams[i].SourceFile);
            }
            return dictionary;
        }


        public static List<ImageTensor> AsImageTensors(this List<ImageInput> imageInputs)
        {
            return [.. imageInputs.Select(x => x.AsImageTensor())];
        }


        public static JsonElement ToJsonElement(this XElement element)
        {
            var value = element.Value.Trim();
            try
            {
                using (var doc = JsonDocument.Parse(value))
                {
                    return doc.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(value);
            }
        }


        /// <summary>
        /// Determines whether the progress is a file download progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        public static bool IsDownload(this Amuse.Common.PipelineProgress progress)
        {
            return progress.Key == "Download";
        }


        /// <summary>
        /// Gets the download percent text, e.g. "49%", null if the size is unknown.
        /// </summary>
        /// <param name="progress">The download progress, sizes in KB.</param>
        public static string GetDownloadPercentText(this Amuse.Common.PipelineProgress progress)
        {
            if (progress.BatchMaximum <= 0)
                return null;

            return $"{progress.BatchValue * 100L / progress.BatchMaximum}%";
        }


        /// <summary>
        /// Gets the download size text, e.g. "12.3 MB / 25.0 MB".
        /// </summary>
        /// <param name="progress">The download progress, sizes in KB.</param>
        public static string GetDownloadSizeText(this Amuse.Common.PipelineProgress progress)
        {
            if (progress.BatchMaximum <= 0)
                return FormatSizeKB(progress.BatchValue);

            return $"{FormatSizeKB(progress.BatchValue)} / {FormatSizeKB(progress.BatchMaximum)}";
        }


        /// <summary>
        /// Gets the download speed text, e.g. "12.3 MB/s", null if the speed is unknown.
        /// </summary>
        /// <param name="progress">The download progress, speed (KB/s) in Elapsed.</param>
        public static string GetDownloadSpeedText(this Amuse.Common.PipelineProgress progress)
        {
            if (progress.Elapsed <= 0)
                return null;

            return progress.Elapsed < 1024
                ? $"{progress.Elapsed:N0} KB/s"
                : $"{progress.Elapsed / 1024f:N1} MB/s";
        }


        /// <summary>
        /// Gets the download time remaining text, e.g. "1m 20s remaining", null if unknown.
        /// </summary>
        /// <param name="progress">The download progress, sizes in KB, speed (KB/s) in Elapsed.</param>
        public static string GetDownloadRemainingText(this Amuse.Common.PipelineProgress progress)
        {
            var remainingKB = progress.BatchMaximum - progress.BatchValue;
            if (progress.Elapsed <= 0 || progress.BatchMaximum <= 0 || remainingKB <= 0)
                return null;

            var remaining = AppDefault.Remaining.ToLower();
            var timeSpan = TimeSpan.FromSeconds(remainingKB / progress.Elapsed);
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours}h {remaining}";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m {remaining}";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s {remaining}";

            return $"{timeSpan.Seconds}s {remaining}";
        }


        /// <summary>
        /// Formats a size in KB as MB or GB.
        /// </summary>
        /// <param name="sizeKB">The size in KB.</param>
        private static string FormatSizeKB(int sizeKB)
        {
            return sizeKB < 1024 * 1024
                ? $"{sizeKB / 1024f:N1} MB"
                : $"{sizeKB / (1024f * 1024f):N2} GB";
        }
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


    public static partial class RegexManager
    {
        [GeneratedRegex(@"^```", RegexOptions.Multiline)]
        private static partial Regex UnclosedFence { get; }

        [GeneratedRegex(@"(?<=\{|,)\s*([a-zA-Z_][a-zA-Z0-9_*]*)\s*:", RegexOptions.Compiled)]
        private static partial Regex UnquotedKeyRegex { get; }

        [GeneratedRegex(@"<(function|parameter)=([^>]+)>", RegexOptions.Compiled)]
        private static partial Regex UnNamedXmlKeyRegex { get; }

        [GeneratedRegex(@"^call:(?<name>[\w\-]+)(?<args>\{.*\}?)$", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex ParseGemma4ToolCallRegex { get; }

        public static bool HasUnclosedFence(string markdown)
        {
            int count = UnclosedFence.Count(markdown);
            return count % 2 != 0;
        }

        public static string ReplaceUnquotedKeys(string input)
        {
            return UnquotedKeyRegex.Replace(input.Replace("\\", "\\\\"), "\"$1\":");
        }

        public static string ReplaceUnNamedXmlKeys(string input)
        {
            return UnNamedXmlKeyRegex.Replace(input, "<$1 name=\"$2\">");
        }

        public static Match ParseGemma4ToolCall(string input)
        {
            return ParseGemma4ToolCallRegex.Match(input);
        }
    }
}
