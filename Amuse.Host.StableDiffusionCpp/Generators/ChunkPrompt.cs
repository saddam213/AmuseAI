using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Amuse.Host.StableDiffusionCpp
{
    /// <summary>
    /// Splits a prompt into the prompts of a video generated in chunks.
    /// The text before the first [ChunkN] tag is the initial prompt and is used by every chunk, the text under a tag is added to it from chunk N until another tag takes over.
    /// A tag with no text under it returns to the initial prompt alone.
    /// </summary>
    internal static partial class ChunkPrompt
    {
        /// <summary>
        /// Gets the prompt of a chunk, a prompt without tags is returned unchanged.
        /// </summary>
        /// <param name="prompt">The full prompt.</param>
        /// <param name="chunk">The chunk number, starting at 1.</param>
        public static string GetPrompt(string prompt, int chunk)
        {
            if (string.IsNullOrEmpty(prompt))
                return prompt;

            var tags = TagRegex().Matches(prompt);
            if (tags.Count == 0)
                return prompt;

            var initial = prompt[..tags[0].Index].Trim();
            var section = GetSection(GetSections(prompt, tags), chunk);
            if (section == null || section.Content.Length == 0)
                return initial;
            if (initial.Length == 0)
                return section.Content;

            return $"{initial}\n{section.Content}";
        }


        /// <summary>
        /// Gets the tagged sections of the prompt in the order they were written.
        /// </summary>
        /// <param name="prompt">The full prompt.</param>
        /// <param name="tags">The tags found in the prompt.</param>
        private static List<Section> GetSections(string prompt, MatchCollection tags)
        {
            var sections = new List<Section>();
            for (var i = 0; i < tags.Count; i++)
            {
                var start = tags[i].Index + tags[i].Length;
                var end = i + 1 < tags.Count ? tags[i + 1].Index : prompt.Length;
                if (!int.TryParse(tags[i].Groups[1].Value, out var number))
                    continue;

                sections.Add(new Section(number, prompt[start..end].Trim()));
            }
            return sections;
        }


        /// <summary>
        /// Gets the section in effect for a chunk, the one with the highest number that is not above the chunk, the later one when a number is repeated.
        /// </summary>
        /// <param name="sections">The sections.</param>
        /// <param name="chunk">The chunk number.</param>
        private static Section GetSection(List<Section> sections, int chunk)
        {
            var result = default(Section);
            foreach (var section in sections)
            {
                if (section.Number > chunk)
                    continue;
                if (result == null || section.Number >= result.Number)
                    result = section;
            }
            return result;
        }


        [GeneratedRegex(@"\[\s*chunk\s*(\d+)\s*\]", RegexOptions.IgnoreCase)]
        private static partial Regex TagRegex();


        /// <summary>
        /// A tagged section of a prompt.
        /// </summary>
        /// <param name="Number">The chunk number the section starts at.</param>
        /// <param name="Content">The text of the section.</param>
        private sealed record Section(int Number, string Content);
    }
}
