﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CodeHollow.FeedReader;
using NLog;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RSSBot {
    public static class Commands {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async void Welcome(Message message, GroupCollection matches)
        {
            await Bot.BotClient.SendTextMessageAsync(
                message.Chat,
                "<b>Willkommen beim RSS-Bot!</b>\nSende /help, um zu starten.",
                ParseMode.Html
            );
        }

        public static async void Help(Message message, GroupCollection matches)
        {
            await Bot.BotClient.SendTextMessageAsync(
                message.Chat,
                "<b>/rss</b> <i>[Chat]</i>: Abonnierte Feeds anzeigen\n" +
                "<b>/sub</b> <i>Feed-URL</i> <i>[Chat]</i>: Feed abonnieren\n" +
                "<b>/del</b> <i>Feed-URL</i> <i>[Chat]</i>: Feed löschen\n" +
                "<b>/show</b> <i>Feed-URL</i> <i>[Chat]</i>: Feeds auf dieser Seite anzeigen\n" +
                "<i>[Chat]</i> ist ein optionales Argument mit dem @Kanalnamen.",
                ParseMode.Html
            );
        }

        public static async void Subscribe(Message message, GroupCollection args)
        {
            string url = args[1].Value;
            long chatId = message.Chat.Id;
            RssBotFeed feed = new RssBotFeed(url);

            await Bot.BotClient.SendChatActionAsync(message.Chat, ChatAction.Typing);

            if (args.Count > 2) {
                Chat chatInfo;
                string chatName = args[2].Value;
                bool isId = long.TryParse(chatName, out chatId);

                if (isId) {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatId);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                } else {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatName);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                }

                chatId = chatInfo.Id;

                if (!await Utils.IsBotAdmin(chatId)) {
                    await Bot.BotClient.SendTextMessageAsync(message.Chat,
                        "❌ Du musst den Bot als Administrator zu diesem Kanal hinzufügen.");
                    return;
                }
            }

            try {
                await feed.Check();
            } catch {
                await Bot.BotClient.SendTextMessageAsync(
                    message.Chat,
                    "❌ Kein gültiger RSS-Feed."
                );
                return;
            }

            // Check if we already have the feed
            RssBotFeed existingFeed = Bot.RssBotFeeds
                .FirstOrDefault(x => x.Url.ToLower().Equals(feed.Url.ToLower()));
            if (existingFeed == null) {
                Bot.RssBotFeeds.Add(feed);
            } else {
                feed = existingFeed;
            }

            // Check if chat already subscribed
            if (feed.Subs.Contains(chatId)) {
                await Bot.BotClient.SendTextMessageAsync(message.Chat, "✅ Dieser Feed wurde bereits abonniert.");
            } else {
                feed.Subs.Add(chatId);
                await Bot.BotClient.SendTextMessageAsync(message.Chat, "✅ Feed abonniert!");
                Bot.Save();
            }
        }

        public static async void Unsubscribe(Message message, GroupCollection args)
        {
            string url = args[1].Value;
            long chatId = message.Chat.Id;
            RssBotFeed feed = Bot.RssBotFeeds
                .FirstOrDefault(x => x.Url.ToLower().Equals(url.ToLower()));

            if (args.Count > 2) {
                Chat chatInfo;
                string chatName = args[2].Value;
                bool isId = long.TryParse(chatName, out chatId);

                if (isId) {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatId);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                } else {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatName);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                }

                chatId = chatInfo.Id;

                if (!await Utils.IsBotAdmin(chatId)) {
                    await Bot.BotClient.SendTextMessageAsync(message.Chat,
                        "❌ Du musst den Bot als Administrator zu diesem Kanal hinzufügen.");
                    return;
                }
            }

            if (feed == null || !feed.Subs.Contains(chatId)) {
                await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Feed wurde nicht abonniert.");
                return;
            }

            feed.Cleanup(chatId);
            if (feed.Subs.Count == 0) {
                Bot.RssBotFeeds.Remove(feed);
            }

            await Bot.BotClient.SendTextMessageAsync(message.Chat, "✅ Feed deabonniert!");
            Bot.Save();
        }

        public static async void Show(Message message, GroupCollection args)
        {
            long chatId = message.Chat.Id;
            string chatTitle = message.Chat.Type.Equals(ChatType.Private) ? message.Chat.FirstName : message.Chat.Title;
            await Bot.BotClient.SendChatActionAsync(message.Chat, ChatAction.Typing);

            if (args.Count > 1) {
                Chat chatInfo;
                string chatName = args[1].Value;
                bool isId = long.TryParse(chatName, out chatId);

                if (isId) {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatId);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                } else {
                    try {
                        chatInfo = await Bot.BotClient.GetChatAsync(chatName);
                    } catch {
                        await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Dieser Kanal existiert nicht.");
                        return;
                    }
                }

                chatId = chatInfo.Id;
                chatTitle = chatInfo.Title;

                if (!await Utils.IsBotAdmin(chatId)) {
                    await Bot.BotClient.SendTextMessageAsync(message.Chat,
                        "❌ Du musst den Bot als Administrator zu diesem Kanal hinzufügen.");
                    return;
                }
            }

            List<RssBotFeed> feeds = Bot.RssBotFeeds.Where(x => x.Subs.Contains(chatId)).ToList();

            StringBuilder text = new StringBuilder();
            if (feeds.Count < 1) {
                text.Append("❌ Keine Feeds abonniert.");
            } else {
                text.Append($"<strong>{HttpUtility.HtmlEncode(chatTitle)}</strong> hat abonniert:\n");
                for (int i = 0; i < feeds.Count; i++) {
                    text.Append($"<strong>{i + 1}</strong>) {feeds[i].Url}\n");
                }
            }

            await Bot.BotClient.SendTextMessageAsync(message.Chat, text.ToString(), ParseMode.Html, true);
        }

        public static async void Sync()
        {
            Logger.Info("================================");
            bool hadEntries = false;
            foreach (RssBotFeed feed in Bot.RssBotFeeds.ToList()) {
                Logger.Info(feed.Url);
                try {
                    await feed.Check();
                } catch (Exception e) {
                    Logger.Warn($"FEHLER: {e.Message}");
                    continue;
                }

                if (feed.NewEntries.Count == 0) {
                    Logger.Info("Keine neuen Beiträge.");
                    continue;
                }

                hadEntries = true;
                Logger.Info(feed.NewEntries.Count == 1
                    ? "1 neuer Beitrag"
                    : $"{feed.NewEntries.Count} neue Beiträge");

                foreach (FeedItem entry in feed.NewEntries) {
                    string postTitle = "Kein Titel";
                    if (!string.IsNullOrWhiteSpace(entry.Title)) {
                        postTitle = Utils.StripHtml(entry.Title);
                        postTitle = Utils.EscapeHtml(postTitle);
                    }

                    string postLink = feed.MainLink;
                    string linkName = postLink;
                    if (!string.IsNullOrWhiteSpace(entry.Link)) {
                        postLink = entry.Link;
                        // FeedProxy URLs
                        GroupCollection feedProxy =
                            Utils.ReturnMatches(postLink, "^https?://feedproxy.google.com/~r/(.+?)/.*");
                        linkName = feedProxy.Count > 1 ? feedProxy[1].Value : new Uri(postLink).Host;
                    }

                    // Remove "www."
                    int index = linkName.IndexOf("www.", StringComparison.Ordinal);
                    if (index > -1) {
                        linkName = linkName.Remove(index, 4);
                    }

                    string content = "";
                    if (!string.IsNullOrWhiteSpace(entry.Content)) {
                        content = Utils.ProcessContent(entry.Content);
                    } else if (!string.IsNullOrWhiteSpace(entry.Description)) {
                        content = Utils.ProcessContent(entry.Description);
                    }

                    string text = $"<b>{postTitle}</b>\n<i>{feed.Title}</i>\n{content}";
                    text += $"\n<a href=\"{postLink}\">Weiterlesen auf {linkName}</a>";

                    // Send
                    foreach (long chatId in feed.Subs.ToList()) {
                        await SendFinishedMessage(chatId, text, feed);
                    }
                }
            }

            Logger.Info("Nächster Check in 60 Sekunden");

            if (hadEntries) {
                Bot.Save();
            }

            Bot.JobQueue.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(-1));
        }

        private static async Task SendFinishedMessage(long chatId, string text, RssBotFeed feed, int waitTime = 10)
        {
            try {
                await Bot.BotClient.SendTextMessageAsync(chatId, text, ParseMode.Html, true, true);
                Thread.Sleep(1000);
            } catch (ApiRequestException e) {
                if (e.ErrorCode.Equals(403)) {
                    Logger.Warn(e.Message);
                    feed.Cleanup(chatId);
                    if (feed.Subs.Count == 0) // was last subscriber
                    {
                        Bot.RssBotFeeds.Remove(feed);
                    }
                } else {
                    Logger.Error($"{e.ErrorCode}: {e.Message}");
                }
            } catch (HttpRequestException) // likely 429
            {
                Logger.Warn($"Rate-Limit erreicht, warte {waitTime} Sekunden...");
                Thread.Sleep(waitTime * 1000);
                await SendFinishedMessage(chatId, text, feed, waitTime * 2);
            }
        }

        public static async void ShowAvailableFeeds(Message message, GroupCollection args)
        {
            string url = args[1].Value;
            IEnumerable<HtmlFeedLink> feeds;
            try {
                feeds = await FeedReader.GetFeedUrlsFromUrlAsync(url);
            } catch {
                await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Sete konnte nicht erreicht werden.");
                return;
            }

            List<HtmlFeedLink> htmlFeedLinks = feeds.ToList();
            if (htmlFeedLinks.Count == 0) {
                await Bot.BotClient.SendTextMessageAsync(message.Chat, "❌ Keine Feeds gefunden.");
                return;
            }

            string text = htmlFeedLinks.Aggregate("Feeds gefunden:\n",
                (current, feedLink) =>
                    current + $"* <a href=\"{feedLink.Url}\">{Utils.StripHtml(feedLink.Title)}</a>\n");

            await Bot.BotClient.SendTextMessageAsync(message.Chat, text, ParseMode.Html, true);
        }
    }
}