using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public static class BotCommandUtilities
    {
        public const RegexOptions DefaultRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

        public static bool TryGetCommandValue(this ChatMessage message, BotOptions options, out string commandValue)
            => TryGetCommandValue(message, options.CommandPrefix, options.RequirePrefixInPrivate, out commandValue);

        public static bool TryGetCommandValue(this ChatMessage message, string prefix, bool requirePrefixInPrivate, out string commandValue)
        {
            commandValue = null;
            if (!message.IsText)
                return false;
            if (message.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                commandValue = message.Text.Remove(0, prefix.Length);
                return true;
            }
            else if (message.IsPrivateMessage && !requirePrefixInPrivate)
            {
                commandValue = message.Text;
                return true;
            }
            else
                return false;
        }

        public static Task SendGroupMembersBugNoticeAsync(this IWolfClient client, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return client.ReplyTextAsync(message, "/alert WOLF servers are broken and refused to let me check if you're an admin in this group.\n" +
                "Please tell WOLF server developers to fix this already. :(", cancellationToken);
        }
    }
}
