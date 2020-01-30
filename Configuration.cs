using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IniParser;
using IniParser.Model;
using NLog;
using NLog.Config;
using NLog.Targets;
using StackExchange.Redis;

namespace RSSBot {
    public static class Configuration {
        public static string BotToken;
        private static ConnectionMultiplexer _redis;
        public static IDatabase Database;
        public static string RedisHash;
        public static List<int> Admins;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void Parse() {
            if (!File.Exists("NLog.config")) {
                Console.WriteLine("NLog.config nicht gefunden, setze auf INFO...");
                LoggingConfiguration config = new LoggingConfiguration();

                ConsoleTarget logconsole = new ConsoleTarget("logconsole");
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
                logconsole.Layout = "${longdate} - ${logger} - ${level:uppercase=true} - ${message}";
                LogManager.Configuration = config;
            } else {
                LogManager.LoadConfiguration("NLog.config");
            }

            FileIniDataParser parser = new FileIniDataParser();
            if (!File.Exists("config.ini")) {
                Logger.Fatal("config.ini nicht gefunden.");
                Environment.Exit(1);
            }

            IniData data = parser.ReadFile("config.ini");

            BotToken = data["DEFAULT"]["token"];
            string host = data["REDIS"]["host"] ?? "127.0.0.1";
            string port = data["REDIS"]["port"] ?? "6379";
            string configuration = data["REDIS"]["configuration"] ?? $"{host}:{port}";

            RedisHash = data["REDIS"]["hash"] ?? "telegram:rssbot";
            int db = 0;
            try {
                db = int.Parse(data["REDIS"]["db"] ?? "0");
            } catch (FormatException) {
                Logger.Fatal("Keine valide Datenbanknummer.");
                Environment.Exit(1);
            }

            string admins = data["ADMIN"]["id"];

            if (string.IsNullOrWhiteSpace(BotToken)) {
                Logger.Fatal("Bitte Bot-Token in der config.ini angeben.");
                Environment.Exit(1);
            }

            try {
                Admins = admins.Split(",").Select(int.Parse).ToList();
            } catch (FormatException) {
                Logger.Fatal("Admin-IDs sind keine Integer.");
                Environment.Exit(1);
            }

            Logger.Info("Verbinde mit Redis...");
            // TODO: Sockets
            try {
                _redis = ConnectionMultiplexer.Connect(configuration);
            } catch (RedisConnectionException) {
                Logger.Fatal("Redis-Verbindung fehlgeschlagen.");
                Environment.Exit(1);
            }

            Database = _redis.GetDatabase(db);
        }
    }
}