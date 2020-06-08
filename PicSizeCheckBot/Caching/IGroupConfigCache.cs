using TehGM.WolfBots.Caching;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IGroupConfigCache : IEntityCache<uint, GroupConfig> { }
    public class GroupConfigCache : EntityCache<uint, GroupConfig>, IGroupConfigCache { }
}
