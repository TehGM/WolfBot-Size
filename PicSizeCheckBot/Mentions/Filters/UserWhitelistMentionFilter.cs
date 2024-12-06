using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    [BsonDiscriminator("Mentions.Filters.UserWhitelist", Required = true)]
    public class UserWhitelistMentionFilter : IMentionFilter
    {
        public HashSet<uint> UserIDs { get; }

        [BsonConstructor(nameof(this.UserIDs))]
        public UserWhitelistMentionFilter(IEnumerable<uint> userIDs)
        {
            this.UserIDs = userIDs as HashSet<uint> ?? userIDs?.ToHashSet();
        }

        public ValueTask<bool> PassesAsync(uint userID, ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default)
        {
            if (this.UserIDs == null)
                return ValueTask.FromResult(false);

            return ValueTask.FromResult(message.SenderID != null && this.UserIDs.Contains(message.SenderID.Value));
        }
    }
}
