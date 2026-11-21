using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class WebPageTool : ToolCallBase
    {
        private const string TavilyExtractUrl = "https://api.tavily.com/extract";
        private const int MaxDownloadChars = 4_000_000;

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "web_page";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Read the content of a web page as text or markdown.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "Web Page Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f15c";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 31;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool to open a link from web_search results or a url the user provides, when you need the full page content rather than a snippet.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "url": {
                            "type": "string",
                            "description": "The absolute http or https url of the page."
                        },
                        "max_chars": {
                            "type": "integer",
                            "description": "The maximum number of characters of page content to return.",
                            "default": 16000
                        }
                    },
                    "required": ["url"]
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
                var uri = ToolHelper.GetWebUri(GetArgument<string>("url"));
                var maxChars = Math.Clamp(GetArgumentOrDefault("max_chars", 16000), 1000, 100000);

                // Tavily (markdown)
                var tavilyToken = settings.AccessTokens?.FirstOrDefault(x => x.Name.Equals("Tavily", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(x.Token))?.Token;
                if (!string.IsNullOrEmpty(tavilyToken))
                {
                    var tavilyResult = await ExtractTavilyAsync(uri, tavilyToken, maxChars, cancellationToken);
                    if (tavilyResult != null)
                        return SuccessResult(tavilyResult);
                }

                // Direct (plain text)
                var directResult = await ExtractDirectAsync(uri, maxChars, cancellationToken);
                return SuccessResult(directResult);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Extracts the page as markdown using the Tavily extract api.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="token">The token.</param>
        /// <param name="maxChars">The maximum chars.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result, or null if Tavily could not extract the page.</returns>
        private async Task<WebPageResult> ExtractTavilyAsync(Uri uri, string token, int maxChars, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, TavilyExtractUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = JsonContent.Create(new
                    {
                        urls = new[] { uri.AbsoluteUri },
                        format = "markdown",
                        extract_depth = "basic"
                    });

                    using (var response = await HttpClient.SendAsync(request, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)))
                        {
                            if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                                return null;

                            var content = results[0].TryGetProperty("raw_content", out var rawContent) ? rawContent.GetString() : null;
                            if (string.IsNullOrWhiteSpace(content))
                                return null;

                            var text = ToolHelper.Truncate(content.Trim(), maxChars, out var isTruncated);
                            return new WebPageResult(uri.AbsoluteUri, GetMarkdownTitle(content), "Tavily", text, isTruncated);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebPage] Tavily extract failed, falling back to direct: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Downloads the page and converts it to plain text.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="maxChars">The maximum chars.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<WebPageResult> ExtractDirectAsync(Uri uri, int maxChars, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.5");
                using (var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
                    if (!mediaType.StartsWith("text/") && !mediaType.Contains("html") && !mediaType.Contains("xml") && !mediaType.Contains("json"))
                        throw new NotSupportedException($"Unsupported content type '{mediaType}'.");

                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (body.Length > MaxDownloadChars)
                        body = body[..MaxDownloadChars];

                    var isHtml = mediaType.Contains("html");
                    var title = isHtml ? ToolHelper.HtmlTitle(body) : null;
                    var content = isHtml ? ToolHelper.HtmlToText(body) : body.Trim();
                    var text = ToolHelper.Truncate(content, maxChars, out var isTruncated);
                    return new WebPageResult(response.RequestMessage?.RequestUri?.AbsoluteUri ?? uri.AbsoluteUri, title, "Direct", text, isTruncated);
                }
            }
        }


        /// <summary>
        /// Gets the first markdown heading as the title.
        /// </summary>
        /// <param name="markdown">The markdown.</param>
        private static string GetMarkdownTitle(string markdown)
        {
            return markdown
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(x => x.StartsWith("# "))?[2..]
                .Trim();
        }


        /// <summary>
        /// WebPageResult structure.
        /// </summary>
        private sealed record WebPageResult(string Url, string Title, string Source, string Content, bool IsTruncated);
    }
}
