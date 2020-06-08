using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IUserDataCache : IEntityCache<uint, UserData> { }
    public class UserDataCache : EntityCache<uint, UserData>, IUserDataCache
    {
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        public UserDataCache(IOptionsMonitor<CachingOptions> cachingOptions)
        {
            this._cachingOptions = cachingOptions;
        }

        protected override bool IsEntityExpired(CachedEntity<UserData> entity)
            => entity.CachingTimeUtc + _cachingOptions.CurrentValue.UserDataCacheLifetime < DateTime.UtcNow;
    }
}
