using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Mentions;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class MentionConfigCache : EntityCache<uint, MentionConfig>, IMentionConfigCache
    {
        public const string OptionName = "MentionConfig";

        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        private bool _enabled => _cachingOptions.Get(OptionName).Enable;
        private TimeSpan _expirationTime => _cachingOptions.Get(OptionName).Lifetime;

        public MentionConfigCache(IOptionsMonitor<CachingOptions> cachingOptions) : base()
        {
            this._cachingOptions = cachingOptions;
        }

        public override void AddOrReplace(MentionConfig entity)
        {
            if (!_enabled) return;
            base.AddOrReplace(entity);
        }

        protected override bool IsEntityExpired(CachedEntity<MentionConfig> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
        }
    }
}
