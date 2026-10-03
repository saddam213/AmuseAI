using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class WorldNewsTool : RssFeedToolBase
    {
        private readonly Feed[] _newsFeeds;

        public WorldNewsTool()
        {
            _newsFeeds =
            [
                new Feed("Sky News","https://feeds.skynews.com/feeds/rss/home.xml"),
                new Feed("BBC News","https://feeds.bbci.co.uk/news/world/rss.xml"),
                new Feed("The Guardian","https://www.theguardian.com/world/rss"),
            ];
        }

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "world_news";

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => """
        {
            "type": "function",
            "function": {
                "name": "world_news",
                "description": "Get the latest world news from multiple news sources. Use this tool when you need a current overview of major world news stories. The tool returns news stories as JSON, including the source, title, publication date, link, and description.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "source": {
                            "type": "string",
                            "enum": ["all", "sky_news", "bbc_news", "the_guardian"],
                            "description": "The news source to retrieve stories from.",
                            "default": "all"
                        },
                        "count": {
                            "type": "integer",
                            "description": "The maximum number of news stories to return from each source.",
                            "default": 5
                        }
                    },
                    "required": []
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
                var count = Arguments.TryGetValue("count", out var countArgument) ? Math.Clamp(countArgument.GetInt32(), 1, 10) : 5;
                var source = Arguments.TryGetValue("source", out var sourceArgument) ? sourceArgument.GetString() : "all";
                var feeds = source switch
                {
                    "sky_news" => _newsFeeds.Where(x => x.Source == "Sky News"),
                    "bbc_news" => _newsFeeds.Where(x => x.Source == "BBC News"),
                    "the_guardian" => _newsFeeds.Where(x => x.Source == "The Guardian"),
                    _ => _newsFeeds
                };

                var results = new List<FeedResult>();
                foreach (var feed in feeds)
                {
                    results.AddRange(await GetFeedAsync(feed, count));
                }
                return JsonSerializer.Serialize(results);
            }
            catch (Exception ex)
            {
                return $"[Error] {Name} tool failed to execute: {ex.Message}";
            }
        }
    }
}
