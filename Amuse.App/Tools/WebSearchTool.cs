using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLookup;

namespace Amuse.App.Tools
{
    public sealed class WebSearchTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "web_search";

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => """
        {
            "type": "function",
            "function": {
                "name": "web_search",
                "description": "Search the public web to find relevant pages, websites, and sources. Use this tool when you need to find URLs or locate current, time-sensitive, unfamiliar, or externally verifiable information. The tool returns a list of search results containing titles and URLs. Prefer specific search queries that clearly describe what you are looking for. Do not use this tool when you can answer the user's question confidently from your existing knowledge.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The search query"
                        },
                        "count": {
                            "type": "integer",
                            "description": "The maximum number of search results to return.",
                            "default": 5
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
                var query = Arguments["query"].GetString();
                var count = Arguments.TryGetValue("count", out var countArgument) ? countArgument.GetInt32() : 5;
                using (var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), HttpClient))
                {
                    var results = await provider.SearchAsync(query, count, cancellationToken);
                    return JsonSerializer.Serialize(results);
                }
            }
            catch (Exception ex)
            {
                return $"[Error] {Name} tool failed to execute: {ex.Message}";
            }
        }
    }
}
