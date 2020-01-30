using System;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace RSSBot {
    /// <summary>
    /// RegexHandler for Telegram Bots.
    /// </summary>
    public class RegexHandler {
        private string Pattern;
        private Action<Message, GroupCollection> CallbackFunction;

        /// <summary>
        /// Constructor for the RegexHandler.
        /// </summary>
        /// <param name="pattern">Regex pattern</param>
        /// <param name="callback">Callback function to call when the update should be processed</param>
        public RegexHandler(string pattern, Action<Message, GroupCollection> callback) {
            Pattern = pattern;
            CallbackFunction = callback;
        }

        /// <summary>
        /// Checks whether the update should be handled by this handler.
        /// </summary>
        /// <param name="message">Telegram Message object</param>
        /// <returns>true if the update should be handled</returns>
        public bool HandleUpdate(Message message) {
            return Regex.IsMatch(message.Text,
                Pattern,
                RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Calls the assoicated callback function.
        /// </summary>
        /// <param name="message">Telegram Message object</param>
        public void ProcessUpdate(Message message) {
            GroupCollection matches = Regex.Match(message.Text,
                Pattern,
                RegexOptions.IgnoreCase
            ).Groups;
            CallbackFunction(message, matches);
        }
    }
}