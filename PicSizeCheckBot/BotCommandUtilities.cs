using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public static class BotCommandUtilities
    {
        public static Task SendGroupMembersBugNoticeAsync(this IWolfClient client, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return client.ReplyTextAsync(message, "/alert WOLF servers are broken and refused to let me check if you're an admin in this group.\n" +
                "Please tell WOLF server developers to fix this already. :(", cancellationToken);
        }
    }
}
