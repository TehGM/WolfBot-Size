using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public static class UserDataStoreExtensions
    {
        public static Task SetUserDataAsync(this IUserDataStore store, UserData data, CancellationToken cancellationToken = default)
            => store.SetUserDataAsync(data, true, cancellationToken);
    }
}
