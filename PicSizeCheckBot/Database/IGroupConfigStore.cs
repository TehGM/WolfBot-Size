using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public interface IGroupConfigStore : IBatchingStore
    {
        Task<GroupConfig> GetGroupConfigAsync(uint groupID, CancellationToken cancellationToken = default);
        Task SetGroupConfigAsync(GroupConfig config, bool instant, CancellationToken cancellationToken = default);
    }
}
