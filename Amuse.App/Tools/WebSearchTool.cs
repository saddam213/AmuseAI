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
                "description": "Search the public web for information. Use this tool when you need information that is current, time-sensitive, unfamiliar, or not available in your existing knowledge. The tool returns a list of search results containing titles, URLs, and descriptions. Use the returned URLs with web_fetch when you need to read the full contents of a specific page. Prefer specific search queries that describe exactly what information you need. Do not use this tool when you can answer the user's question confidently without external information.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The search query"
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
            var query = Arguments["query"].GetString();
            using (var provider = new DuckDuckGoSearchProvider())
            {
                var results = await provider.SearchAsync(query, count: 5, cancellationToken: cancellationToken);
                return JsonSerializer.Serialize(results);
            }
        }
    }
}
