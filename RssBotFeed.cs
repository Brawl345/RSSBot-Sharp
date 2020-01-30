using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeHollow.FeedReader;

namespace RSSBot {
    public class RssBotFeed {
        public readonly string Url;
        public string LastEntry;
        public readonly HashSet<long> Subs = new HashSet<long>();
        public string MainLink { get; private set; }
        public string Title { get; private set; }
        public List<FeedItem> NewEntries { get; private set; }
        
        public RssBotFeed(string url, string lastEntry = null, HashSet<long> subs = null) {
            Url = url;
            if (!string.IsNullOrWhiteSpace(lastEntry)) {
                LastEntry = lastEntry;
            }

            if (subs != null) {
                Subs = subs;
            }
        }

        public async Task Check() {
            Feed feed = await FeedReader.ReadAsync(Url);
            if (string.IsNullOrWhiteSpace(feed.Link)) {
                throw new Exception("Kein gültiger RSS-Feed.");
            }

            MainLink = feed.Link;
            Title = feed.Title;

            if (feed.Items == null || feed.Items.Count <= 0) return;
            NewEntries = string.IsNullOrWhiteSpace(LastEntry)
                ? feed.Items.ToList()
                : GetNewEntries(feed.Items);

            LastEntry = string.IsNullOrWhiteSpace(feed.Items.First().Id)
                ? feed.Items.First().Link
                : feed.Items.First().Id;
        }

        private List<FeedItem> GetNewEntries(IEnumerable<FeedItem> entries) {
            List<FeedItem> newEntries = new List<FeedItem>();
            foreach (FeedItem entry in entries) {
                if (!string.IsNullOrWhiteSpace(entry.Id)) {
                    if (entry.Id.Equals(LastEntry)) {
                        break;
                    }

                    newEntries.Add(entry);
                } else {
                    if (entry.Link.Equals(LastEntry)) {
                        break;
                    }

                    newEntries.Add(entry);
                }
            }

            newEntries.Reverse();
            return newEntries;
        }

        public override string ToString() {
            return $"RSS-Feed: '{Url}'";
        }

        public void Cleanup(long chatId) {
            Subs.Remove(chatId);
            string feedKey = $"{Configuration.RedisHash}:{Url}";
            Configuration.Database.SetRemove($"{feedKey}:subs", chatId);

            // No subscribers, delete all references
            if (Subs.Count != 0 || !Configuration.Database.KeyExists(feedKey)) return;
            Configuration.Database.KeyDelete(feedKey);
            Configuration.Database.KeyDelete($"{feedKey}:subs");
            Configuration.Database.SetRemove($"{Configuration.RedisHash}:feeds", Url);
        }
    }
}