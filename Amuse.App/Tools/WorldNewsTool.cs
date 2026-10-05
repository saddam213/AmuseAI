using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class WorldNewsTool : RssFeedToolBase
    {
        private static Feed[] _newsFeeds;

        public WorldNewsTool()
        {
            _newsFeeds ??=
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
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Get the latest world news from multiple news sources, including Sky News, BBC News, and The Guardian.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "World News Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f1ea";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 11;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool for current world news and recent international developments.",
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
                await CheckExternalAccess(settings);
                var count = GetArgumentOrDefault("count", 5);
                var source = GetArgumentOrDefault("source", "all");
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
                return SuccessResult(results);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }
    }
}
