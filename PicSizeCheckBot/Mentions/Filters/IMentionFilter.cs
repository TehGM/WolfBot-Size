using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters
{
    public interface IMentionFilter
    {
        ValueTask<bool> PassesAsync(uint userID, ChatMessage message, IWolfClient client, CancellationToken cancellationToken = default);
    }
}
