using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IGroupConfigCache : IEntityCache<uint, GroupConfig> { }
    public class GroupConfigCache : EntityCache<uint, GroupConfig>, IGroupConfigCache
    {
        public const string OptionName = "GroupConfig";

        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;

        private bool _enabled => _cachingOptions.Get(OptionName).Enable;
        private TimeSpan _expirationTime => _cachingOptions.Get(OptionName).Lifetime;

        public GroupConfigCache(IOptionsMonitor<CachingOptions> cachingOptions)
        {
            this._cachingOptions = cachingOptions;
        }

        public override void AddOrReplace(GroupConfig entity)
        {
            if (!_enabled) return;
            base.AddOrReplace(entity);
        }

        protected override bool IsEntityExpired(CachedEntity<GroupConfig> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
        }
    }
}
