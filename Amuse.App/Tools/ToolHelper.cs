using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Amuse.App.Tools
{
    public static class ToolHelper
    {
        private static readonly Regex _removeBlocksRegex = new Regex(@"<(script|style|noscript|svg|head|template)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _commentRegex = new Regex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _blockTagRegex = new Regex(@"<\s*(br|/p|/div|/li|/tr|/h[1-6]|/section|/article|/header|/footer|/table|/ul|/ol|/blockquote|/pre)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _listItemRegex = new Regex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _tagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex _titleRegex = new Regex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _spacesRegex = new Regex(@"[ \t\f\v ]+", RegexOptions.Compiled);
        private static readonly Regex _newLinesRegex = new Regex(@"\s*\n\s*(\n\s*)+", RegexOptions.Compiled);

        /// <summary>
        /// Converts HTML to readable plain text.
        /// </summary>
        /// <param name="html">The HTML.</param>
        public static string HtmlToText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // Remove non-content
            var text = _commentRegex.Replace(html, string.Empty);
            text = _removeBlocksRegex.Replace(text, string.Empty);

            // Structure
            text = _listItemRegex.Replace(text, "\n- ");
            text = _blockTagRegex.Replace(text, "\n");
            text = _tagRegex.Replace(text, " ");

            // Normalize
            text = WebUtility.HtmlDecode(text).Replace("\r", string.Empty);
            text = _spacesRegex.Replace(text, " ");
            text = _newLinesRegex.Replace(text, "\n\n");
            return text.Trim();
        }


        /// <summary>
        /// Gets the HTML document title.
        /// </summary>
        /// <param name="html">The HTML.</param>
        public static string HtmlTitle(string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var match = _titleRegex.Match(html);
            return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
        }


        /// <summary>
        /// Truncates the text to the maximum number of characters.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="maxChars">The maximum characters.</param>
        /// <param name="isTruncated">if set to <c>true</c> the text was truncated.</param>
        public static string Truncate(string text, int maxChars, out bool isTruncated)
        {
            isTruncated = false;
            if (string.IsNullOrEmpty(text) || maxChars <= 0 || text.Length <= maxChars)
                return text;

            isTruncated = true;
            return text[..maxChars];
        }


        /// <summary>
        /// Validates an absolute http or https url.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <exception cref="System.ArgumentException">Only absolute http or https urls are supported.</exception>
        public static Uri GetWebUri(string url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"'{url}' is not a valid http or https url.");

            return uri;
        }


        /// <summary>
        /// Converts bytes to gigabytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public static double ToGB(double bytes)
        {
            return Math.Round(bytes / 1024d / 1024d / 1024d, 1);
        }
    }
}
