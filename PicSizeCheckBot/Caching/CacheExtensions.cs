using TehGM.WolfBots.Caching;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public static class CacheExtensions
    {
        public static void AddOrReplace(this IEntityCache<uint, UserData> cache, UserData entity)
            => cache.AddOrReplace(entity.ID, entity);

        public static void AddOrReplace(this IEntityCache<uint, GroupConfig> cache, GroupConfig entity)
            => cache.AddOrReplace(entity.ID, entity);
    }
}
