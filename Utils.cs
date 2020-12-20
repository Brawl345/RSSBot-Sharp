using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RSSBot {
    public static class Utils {
        private static readonly Regex RegexHtml = new Regex("<.*?>");

        public static string StripHtml(string input)
        {
            return RegexHtml.Replace(input, string.Empty).Trim();
        }

        public static string EscapeHtml(string input)
        {
            input = input.Replace("<", "&lt;");
            input = input.Replace(">", "&gt;");
            return input;
        }

        private static string CleanRss(string input)
        {
            string[] replacements = {
                "[←]",
                "[…]",
                "[...]",
                "[bilder]",
                "[boerse]",
                "[mehr]",
                "[video]",
                "...[more]",
                "[more]",
                "[liveticker]",
                "[livestream]",
                "[multimedia]",
                "[sportschau]",
                "[phoenix]",
                "[swr]",
                "[ndr]",
                "[mdr]",
                "[rbb]",
                "[wdr]",
                "[hr]",
                "[br]",
                "Click for full.",
                "Read more »",
                "Read more",
                "...Read More",
                "...mehr lesen",
                "mehr lesen",
                "(more…)",
                "View On WordPress",
                "Continue reading →",
                "» weiterlesen",
                "(Feed generated with  FetchRSS)",
                "(RSS generated with  FetchRss)",
                "-- Delivered by Feed43 service",
                "Meldung bei www.tagesschau.de lesen"
            };

            string[] regexReplacements = {
                "Der Beitrag.*erschien zuerst auf .+.",
                "The post.*appeared first on .+.",
                "http://www.serienjunkies.de/.*.html"
            };

            input = replacements.Aggregate(input, (current, replacement) => current.Replace(replacement, ""));
            input = regexReplacements.Aggregate(input,
                (current, replacement) => Regex.Replace(current, replacement, ""));

            return input;
        }

        public static string ProcessContent(string input)
        {
            string content = StripHtml(HttpUtility.HtmlDecode(input));
            content = CleanRss(content);
            if (content.Length > 250) {
                content = content.Substring(0, 250) + "...";
            }

            return content;
        }

        public static GroupCollection ReturnMatches(string text, string pattern)
        {
            return Regex.Match(text,
                pattern,
                RegexOptions.IgnoreCase
            ).Groups;
        }

        public static async Task<bool> IsBotAdmin(long chatId)
        {
            ChatMember chatMember = await Bot.BotClient.GetChatMemberAsync(chatId, Bot.BotClient.BotId);
            return chatMember.Status.Equals(ChatMemberStatus.Administrator);
        }
    }
}