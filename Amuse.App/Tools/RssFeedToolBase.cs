using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Amuse.App.Tools
{
    public abstract class RssFeedToolBase : ToolCallBase
    {
        protected virtual async Task<List<FeedResult>> GetFeedAsync(Feed feed, int count)
        {
            var xml = await HttpClient.GetStringAsync(feed.Link);
            var doc = XDocument.Parse(xml);
            return doc.Descendants("item")
               .Select(x => new FeedResult(feed.Source, (string)x.Element("title"), (string)x.Element("pubDate"), (string)x.Element("link"), (string)x.Element("description")))
               .Take(count)
               .ToList();
        }


        protected record Feed(string Source, string Link);
        protected record FeedResult
        {
            public FeedResult(string source, string title, string published, string link, string description)
            {
                Source = source;
                Link = link?.Trim();
                Title = title?.Trim();
                Description = description?.Trim();
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
