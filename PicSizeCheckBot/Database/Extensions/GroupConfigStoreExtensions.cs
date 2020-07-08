using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public static class GroupConfigStoreExtensions
    {
        public static Task SetGroupConfigAsync(this IGroupConfigStore store, GroupConfig config, CancellationToken cancellationToken = default)
            => store.SetGroupConfigAsync(config, true, cancellationToken);
    }
}
