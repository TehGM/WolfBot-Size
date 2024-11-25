using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    [BsonDiscriminator("Mentions.Filters.GroupBlacklist", Required = true)]
    public class GroupBlacklistMentionFilter : IMentionFilter
    {
        public HashSet<uint> GroupIDs { get; }

        [BsonConstructor(nameof(this.GroupIDs))]
        public GroupBlacklistMentionFilter(IEnumerable<uint> groupIDs)
        {
            this.GroupIDs = groupIDs as HashSet<uint> ?? groupIDs?.ToHashSet();
        }

        public ValueTask<bool> PassesAsync(ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default)
        {
            if (this.GroupIDs == null)
                return ValueTask.FromResult(true);

            return ValueTask.FromResult(!this.GroupIDs.Contains(message.RecipientID));
        }
    }
}
