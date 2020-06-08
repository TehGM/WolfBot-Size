using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class CachingOptions
    {
        public bool CacheUserData { get; set; } = true;
        public bool CacheGroupsConfig { get; set; } = true;

        public TimeSpan UserDataCacheLifetime { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan GroupCacheCacheLifetime { get; set; } = TimeSpan.FromMinutes(30);
    }
}
