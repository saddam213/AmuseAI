using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Search the public web for relevant, authoritative, and up-to-date information.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "Web Search Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "e8a6";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 10;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when an answer requires current, or externally verifiable information, or when you need to find a specific website or page. Do not use it when existing knowledge is sufficient.",
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
                await CheckExternalAccess(settings);
                var query = GetArgument<string>("query");
                var count = GetArgumentOrDefault("count", 5);
                var providers = GetProviders(settings);
                using (var provider = new WebSearchClient(providers))
                {
                    var results = await provider.SearchAsync(query, new WebSearchOptions { MaxResultsPerProvider = count }, cancellationToken);
                    return SuccessResult(results);
                }
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Gets the providers.
        /// </summary>
        /// <param name="settings">The settings.</param>
        private ISearchProvider[] GetProviders(Settings settings)
        {
            var providers = new List<ISearchProvider>();
            foreach (var accessToken in settings.AccessTokens.Where(x => !string.IsNullOrEmpty(x.Token)))
            {
                if (accessToken.Name.Equals("Tavily", StringComparison.OrdinalIgnoreCase))
                    providers.Add(new TavilySearchProvider(new TavilySearchOptions { ApiKey = accessToken.Token }));
                else if (accessToken.Name.Equals("Mojeek", StringComparison.OrdinalIgnoreCase))
                    providers.Add(new MojeekSearchProvider(new MojeekSearchOptions { ApiKey = accessToken.Token }));
                else if (accessToken.Name.Equals("SearchApi", StringComparison.OrdinalIgnoreCase))
                    providers.Add(new SearchApiProvider(new SearchApiOptions { ApiKey = accessToken.Token }));
            }

            if (providers.Count == 0)
            {
                // Fallback to DuckDuckGo (no API key required, but results are poor)
                providers.Add(new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions()));
            }
            return [.. providers];
        }
    }
}
