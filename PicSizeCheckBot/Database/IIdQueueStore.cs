using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public interface IIdQueueStore
    {
        Task<IdQueue> GetIdQueueByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IdQueue> GetIdQueueByOwnerAsync(uint ownerID, CancellationToken cancellationToken = default);
        Task SetIdQueueAsync(IdQueue queue, CancellationToken cancellationToken = default);
    }
}
