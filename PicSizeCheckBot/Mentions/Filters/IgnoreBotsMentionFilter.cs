using MongoDB.Bson.Serialization.Attributes;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    [BsonDiscriminator("Mentions.Filters.IgnoreBots", Required = true)]
    public class IgnoreBotsMentionFilter : IMentionFilter
    {
        public async ValueTask<bool> PassesAsync(ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default)
        {
            if (message.SenderID == null)
                return true;

            WolfUser sender = await client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            return sender == null || (sender.Privileges & WolfPrivilege.Bot) != WolfPrivilege.Bot;
        }
    }
}
