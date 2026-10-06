using Amuse.App.Common;
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


        public static int GetIndex(this MemoryProfile profile, int deviceMemory)
        {
            int bestIndex = -1;
            int bestValue = int.MinValue;

            for (int i = 0; i < profile.MemoryModes.Length; i++)
            {
                int value = profile.MemoryModes[i];
                if (value <= deviceMemory && value >= bestValue)
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


        public static MemoryProfile[] Copy(this MemoryProfile[] collection)
        {
            if (collection.IsNullOrEmpty())
                return [];

            return collection.Select(x => new MemoryProfile
            {
                QualityMode = x.QualityMode,
                MemoryModes = x.MemoryModes.ToArray(),
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
        /// Gets the thinking text.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="tagOpen">The tag open.</param>
        /// <param name="tagClose">The tag close.</param>
        public static string GetThinkingText(string content, bool isConversation = false, string tagOpen = "<think>", string tagClose = "</think>")
        {
            if (isConversation)
                content = GetLastMessage(content);

            if (string.IsNullOrEmpty(content))
                return default;

            if (content.StartsWith(tagOpen, StringComparison.OrdinalIgnoreCase))
            {
                var start = tagOpen.Length;
                var end = content.IndexOf(tagClose, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                    return content[start..end].Trim();
            }
            return default;
        }


        /// <summary>
        /// Gets the response text.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="tagOpen">The tag open.</param>
        /// <param name="tagClose">The tag close.</param>
        /// <returns>System.String.</returns>
        public static string GetResponseText(string content, bool isConversation = false, string tagOpen = "<think>", string tagClose = "</think>")
        {
            if (isConversation)
                content = StripToolCalls(GetLastMessage(content));

            if (string.IsNullOrEmpty(content))
                return string.Empty;

            if (content.StartsWith(tagOpen, StringComparison.OrdinalIgnoreCase))
            {
                var start = content.IndexOf(tagClose, StringComparison.OrdinalIgnoreCase);
                if (start > 0)
                    return StripToolCalls(content[(start + tagClose.Length)..].Trim());
            }
            return StripToolCalls(content);
        }


        public static string GetLastMessage(string content, string tagOpen = "\n<assistant>\n", string tagClose = $"\n</assistant>\n")
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            int end = content.LastIndexOf(tagClose, StringComparison.Ordinal);
            if (end < 0)
                return null;

            int start = content.LastIndexOf(tagOpen, end, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += tagOpen.Length;
            return content[start..end];
        }


        private static string StripToolCalls(string content)
        {
            if (!content.Contains(ToolService.TagClose))
                return content;

            var start = content.LastIndexOf(ToolService.TagClose) + ToolService.TagClose.Length;
            if (start < 0)
                return content;

            return content[start..].Trim('\n');
        }


        public static MemoryProfile[] GetMemoryProfile(double parameterCount)
        {
            const double size4Bit = 0.5;
            const double size8Bit = 1.0;
            const double size16Bit = 2.0;
            const double overhead4Bit = 1.3;
            const double overhead8Bit = 1.1;
            const double overhead16Bit = 1.1;
            return
            [
                new MemoryProfile
                {
                    QualityMode = QualityMode.Draft,
                    MemoryModes = [Estimate(parameterCount, size4Bit, overhead4Bit)]
                },
                 new MemoryProfile
                {
                    QualityMode = QualityMode.Standard,
                    MemoryModes = [Estimate(parameterCount, size8Bit, overhead8Bit)]
                },
                 new MemoryProfile
                {
                    QualityMode = QualityMode.Production,
                    MemoryModes = [Estimate(parameterCount, size16Bit, overhead16Bit)]
                }
            ];
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
