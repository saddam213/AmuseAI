using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class WikipediaTool : ToolCallBase
    {
        private static readonly Regex _languageRegex = new Regex("^[a-z]{2,3}(-[a-z]+)?$", RegexOptions.Compiled);

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "wikipedia";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Search Wikipedia and return article summaries.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => AppDefault.ToolWikipedia;

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f266";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.Brands;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 42;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool for factual, encyclopedic information about people, places, events, concepts or things. Use web_page with the returned url to read the full article.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The topic or article title to look up."
                        },
                        "count": {
                            "type": "integer",
                            "description": "The maximum number of articles to return.",
                            "default": 3
                        },
                        "language": {
                            "type": "string",
                            "description": "The Wikipedia language code, e.g. 'en', 'ja', 'de'.",
                            "default": "en"
                        }
                    },
                    "required": ["query"]
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                await CheckExternalAccess(settings);
                var query = GetArgument<string>("query");
                var count = Math.Clamp(GetArgumentOrDefault("count", 3), 1, 10);
                var language = GetArgumentOrDefault("language", "en")?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(language) || !_languageRegex.IsMatch(language))
                    language = "en";

                // Search
                var keys = await SearchAsync(language, query, count, cancellationToken);
                if (keys.Count == 0)
                    throw new Exception($"No Wikipedia articles found for '{query}'.");

                // Summaries
                var tasks = keys.Select(key => GetSummaryAsync(language, key, cancellationToken)).ToArray();
                var summaries = await Task.WhenAll(tasks);
                var results = summaries.Where(x => x != null).ToList();
                return SuccessResult(results);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Searches for article keys.
        /// </summary>
        /// <param name="language">The language.</param>
        /// <param name="query">The query.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<List<string>> SearchAsync(string language, string query, int count, CancellationToken cancellationToken)
        {
            var url = $"https://{language}.wikipedia.org/w/rest.php/v1/search/page?q={Uri.EscapeDataString(query)}&limit={count}";
            using (var document = JsonDocument.Parse(await GetStringAsync(url, cancellationToken)))
            {
                var results = new List<string>();
                if (document.RootElement.TryGetProperty("pages", out var pages))
                {
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
                            results.Add(key.GetString());
                    }
                }
                return results;
            }
        }


        /// <summary>
        /// Gets the article summary.
        /// </summary>
        /// <param name="language">The language.</param>
        /// <param name="key">The article key.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The summary, or null if unavailable.</returns>
        private async Task<WikipediaResult> GetSummaryAsync(string language, string key, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://{language}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(key)}?redirect=true";
                using (var document = JsonDocument.Parse(await GetStringAsync(url, cancellationToken)))
                {
                    var root = document.RootElement;
                    var pageUrl = root.TryGetProperty("content_urls", out var contentUrls)
                        && contentUrls.TryGetProperty("desktop", out var desktop)
                        && desktop.TryGetProperty("page", out var page) ? page.GetString() : $"https://{language}.wikipedia.org/wiki/{Uri.EscapeDataString(key)}";

                    return new WikipediaResult
                    (
                        GetString(root, "title"),
                        GetString(root, "description"),
                        GetString(root, "extract"),
                        pageUrl
                    );
                }
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }


        /// <summary>
        /// Gets a string property, or null.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The name.</param>
        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }


        /// <summary>
        /// WikipediaResult structure.
        /// </summary>
        private sealed record WikipediaResult(string Title, string Description, string Summary, string Url);
    }
}
