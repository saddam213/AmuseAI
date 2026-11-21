using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Amuse.App.Tools
{
    public abstract class RssFeedToolBase : ToolCallBase
    {
        private const int MaxDescriptionLength = 500;

        /// <summary>
        /// Gets the feed items.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual async Task<List<FeedResult>> GetFeedAsync(Feed feed, int count, CancellationToken cancellationToken = default)
        {
            var xml = await GetStringAsync(feed.Link, cancellationToken);
            var doc = XDocument.Parse(xml);
            return doc.Descendants("item")
               .Select(x => new FeedResult(feed.Source, (string)x.Element("title"), (string)x.Element("pubDate"), (string)x.Element("link"), (string)x.Element("description")))
               .OrderByDescending(x => x.Published)
               .Take(count)
               .ToList();
        }


        /// <summary>
        /// Gets the items from multiple feeds, skipping any feed that fails.
        /// </summary>
        /// <param name="feeds">The feeds.</param>
        /// <param name="count">The count per feed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected async Task<List<FeedResult>> GetFeedsAsync(IEnumerable<Feed> feeds, int count, CancellationToken cancellationToken = default)
        {
            // Fetch
            var feedList = feeds.ToList();
            var tasks = feedList.Select(feed => GetFeedOrDefaultAsync(feed, count, cancellationToken)).ToArray();
            var feedResults = await Task.WhenAll(tasks);

            // Result
            var results = feedResults.SelectMany(x => x).OrderByDescending(x => x.Published).ToList();
            if (results.Count == 0 && feedList.Count > 0)
                throw new Exception("Failed to retrieve any feed items.");
            return results;
        }


        /// <summary>
        /// Gets the feed items, or an empty list if the feed fails.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<List<FeedResult>> GetFeedOrDefaultAsync(Feed feed, int count, CancellationToken cancellationToken)
        {
            try
            {
                return await GetFeedAsync(feed, count, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RssFeed] Failed to read feed {feed.Source}: {ex.Message}");
                return [];
            }
        }


        /// <summary>
        /// Feed structure.
        /// </summary>
        protected record Feed(string Source, string Link);


        /// <summary>
        /// FeedResult structure.
        /// </summary>
        protected record FeedResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FeedResult"/> class.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="title">The title.</param>
            /// <param name="published">The published date.</param>
            /// <param name="link">The link.</param>
            /// <param name="description">The description.</param>
            public FeedResult(string source, string title, string published, string link, string description)
            {
                Source = source;
                Link = link?.Trim();
                Title = ToolHelper.HtmlToText(title);
                Description = ToolHelper.Truncate(ToolHelper.HtmlToText(description), MaxDescriptionLength, out _);
                Published = DateTimeOffset.TryParse(published?.Trim(), out var datetime) ? datetime.UtcDateTime : DateTime.MinValue;
            }

            public string Source { get; set; }
            public string Title { get; set; }
            public DateTime Published { get; set; }
            public string Link { get; set; }
            public string Description { get; set; }
        }
    }
}
