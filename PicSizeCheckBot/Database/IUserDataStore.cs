using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public interface IUserDataStore
    {
        Task<UserData> GetUserDataAsync(uint userID, CancellationToken cancellationToken = default);
        Task SetUserDataAsync(UserData data, bool instant, CancellationToken cancellationToken = default);
    }
}
