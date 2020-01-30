#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RSSBot {
    public static class Bot {
        public static ITelegramBotClient BotClient;
        public static User BotInfo;
        public static readonly List<RssBotFeed> RssBotFeeds = new List<RssBotFeed>();
        public static Timer JobQueue;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static HashSet<RegexHandler> Handlers;

        private static void Main(string[] args) {
            Configuration.Parse();
            BotClient = new TelegramBotClient(Configuration.BotToken);
            try {
                BotInfo = BotClient.GetMeAsync().Result;
            } catch (AggregateException) {
                Logger.Fatal("Login fehlgeschlagen, Bot-Token prüfen!");
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += delegate { Save(); };

            // Read subscribed feeds from Redis
            ReadAllFeeds();

            // Add handlers
            Handlers = new HashSet<RegexHandler> {
                new RegexHandler($"^/start(?:@{BotInfo.Username})?$", Commands.Welcome),
                new RegexHandler($"^/help(?:@{BotInfo.Username})?$", Commands.Help),
                new RegexHandler($"^/rss(?:@{BotInfo.Username})?$", Commands.Show),
                new RegexHandler($"^/rss(?:@{BotInfo.Username})? (@?[A-z0-9_]+)$", Commands.Show),
                new RegexHandler(
                    $"^/show(?:@{BotInfo.Username})? (http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&~+]|[!*(),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+)$",
                    Commands.ShowAvailableFeeds),
                new RegexHandler(
                    $"^/sub(?:@{BotInfo.Username})? (http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&~+]|[!*(),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+)$",
                    Commands.Subscribe),
                new RegexHandler(
                    $"^/sub(?:@{BotInfo.Username})? (http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&~+]|[!*(),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+) (@?[A-z0-9_]+)$$",
                    Commands.Subscribe),
                new RegexHandler(
                    $"^/del(?:@{BotInfo.Username})? (http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&~+]|[!*(),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+)$",
                    Commands.Unsubscribe),
                new RegexHandler(
                    $"^/del(?:@{BotInfo.Username})? (http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&~+]|[!*(),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+) (@?[A-z0-9_]+)$$",
                    Commands.Unsubscribe),
            };

            JobQueue = new Timer(e => { Commands.Sync(); }, null, TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(-1));

            Logger.Info($"Bot gestartet: {BotInfo.FirstName}, AKA {BotInfo.Username} ({BotInfo.Id}).");

            BotClient.OnMessage += Bot_OnMessage;
            BotClient.StartReceiving();
            Console.ReadLine();
            BotClient.StopReceiving();
            Save();
        }

        private static void ReadAllFeeds() {
            RedisValue[] allFeedUrls = Configuration.Database.SetMembers($"{Configuration.RedisHash}:feeds");
            foreach (RedisValue feedUrl in allFeedUrls) {
                HashSet<long> subs = new HashSet<long>();
                RedisValue[] allSubs = Configuration.Database.SetMembers($"{Configuration.RedisHash}:{feedUrl}:subs");
                foreach (RedisValue sub in allSubs) {
                    subs.Add(Convert.ToInt64(sub));
                }

                string lastEntry = Configuration.Database.HashGet($"{Configuration.RedisHash}:{feedUrl}", "last_entry");

                RssBotFeed feed = new RssBotFeed(feedUrl, lastEntry, subs);
                RssBotFeeds.Add(feed);
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e) {
            Save();
        }

        private static void Bot_OnMessage(object? sender, MessageEventArgs messageEventArgs) {
            var message = messageEventArgs.Message;
            if (message == null || message.Type != MessageType.Text) return;
            if (!Configuration.Admins.Contains(message.From.Id)) {
                return;
            }

            foreach (RegexHandler handler in Handlers.Where(handler => handler.HandleUpdate(message))) {
                handler.ProcessUpdate(message);
            }
        }

        public static async void Save() {
            if (RssBotFeeds.Count > 0) {
                Logger.Info("Speichere Daten...");
            }

            foreach (RssBotFeed feed in RssBotFeeds) {
                string feedKey = $"{Configuration.RedisHash}:{feed.Url}";
                if (string.IsNullOrWhiteSpace(feed.LastEntry)) continue;

                await Configuration.Database.HashSetAsync(feedKey, "last_entry", feed.LastEntry);
                foreach (long chatId in feed.Subs) {
                    await Configuration.Database.SetAddAsync($"{feedKey}:subs", chatId);
                }

                await Configuration.Database.SetAddAsync($"{Configuration.RedisHash}:feeds", feed.Url);
            }

            Logger.Info("Gespeichert!");
        }
    }
}