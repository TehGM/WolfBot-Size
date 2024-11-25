using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    [BsonDiscriminator("Mentions.Filters.UserBlacklist", Required = true)]
    public class UserBlacklistMentionFilter : IMentionFilter
    {
        public HashSet<uint> UserIDs { get; }

        [BsonConstructor(nameof(this.UserIDs))]
        public UserBlacklistMentionFilter(IEnumerable<uint> userIDs)
        {
            this.UserIDs = userIDs as HashSet<uint> ?? userIDs?.ToHashSet();
        }

        public ValueTask<bool> PassesAsync(ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default)
        {
            if (this.UserIDs == null)
                return ValueTask.FromResult(true);

            return ValueTask.FromResult(message.SenderID != null && !this.UserIDs.Contains(message.SenderID.Value));
        }
    }
}
