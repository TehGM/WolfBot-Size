using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IUserDataCache : IEntityCache<uint, UserData> { }
    public class UserDataCache : EntityCache<uint, UserData>, IUserDataCache
    {
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        private bool _enabled => _cachingOptions.Get("UserData").Enable;
        private TimeSpan _expirationTime => _cachingOptions.Get("UserData").Lifetime;

        public UserDataCache(IOptionsMonitor<CachingOptions> cachingOptions)
        {
            this._cachingOptions = cachingOptions;
        }

        public override void AddOrReplace(UserData entity)
        {
            if (!_enabled) return;
            base.AddOrReplace(entity);
        }

        protected override bool IsEntityExpired(CachedEntity<UserData> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
        }
    }
}
