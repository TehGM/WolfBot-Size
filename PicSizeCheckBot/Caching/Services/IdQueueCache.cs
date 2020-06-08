using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class IdQueueCache : EntityCache<string, IdQueue>, IIdQueueCache
    {
        public const string OptionName = "IdQueue";

        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        private bool _enabled => _cachingOptions.Get(OptionName).Enable;
        private TimeSpan _expirationTime => _cachingOptions.Get(OptionName).Lifetime;

        public IdQueueCache(IOptionsMonitor<CachingOptions> cachingOptions)
        {
            this._cachingOptions = cachingOptions;
        }

        public override void AddOrReplace(IdQueue entity)
        {
            if (!_enabled) return;
            base.AddOrReplace(entity);
        }

        protected override bool IsEntityExpired(CachedEntity<IdQueue> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
        }
    }
}
