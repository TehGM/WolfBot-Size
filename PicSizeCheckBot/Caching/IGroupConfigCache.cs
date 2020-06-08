using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IGroupConfigCache : IEntityCache<uint, GroupConfig> { }
    public class GroupConfigCache : EntityCache<uint, GroupConfig>, IGroupConfigCache
    {
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        public GroupConfigCache(IOptionsMonitor<CachingOptions> cachingOptions)
        {
            this._cachingOptions = cachingOptions;
        }

        protected override bool IsEntityExpired(CachedEntity<GroupConfig> entity)
            => entity.CachingTimeUtc + _cachingOptions.CurrentValue.GroupCacheCacheLifetime < DateTime.UtcNow;
    }
}
