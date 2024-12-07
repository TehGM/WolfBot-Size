using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    [BsonDiscriminator("Mentions.Filters.RequireGroupPresence", Required = true)]
    public class RequireGroupPresenceMentionFilter : IMentionFilter
    {
        public HashSet<uint> ExceptGroupIDs { get; }

        [BsonConstructor]
        public RequireGroupPresenceMentionFilter()
        {
            this.ExceptGroupIDs = new HashSet<uint>();
        }

        public async ValueTask<bool> PassesAsync(uint userID, ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default)
        {
            if (this.ExceptGroupIDs?.Contains(message.RecipientID) == true)
                return true;

            WolfGroup group = await client.GetGroupAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);
            if (group == null)
                return false;

            if (!group.Members.TryGetValue(userID, out WolfGroupMember member))
                return false;

            return member.Capabilities != WolfGroupCapabilities.Banned && member.Capabilities != WolfGroupCapabilities.NotMember;
        }
    }
}
