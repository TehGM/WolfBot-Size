using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IGroupConfigCache : IEntityCache<uint, GroupConfig> { }
    public class GroupConfigCache : EntityCache<uint, GroupConfig>, IGroupConfigCache, IHostedService
    {
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;
        private readonly ILogger _log;
        private TimeSpan _expirationTime => _cachingOptions.CurrentValue.GroupCacheCacheLifetime;

        public GroupConfigCache(IOptionsMonitor<CachingOptions> cachingOptions, ILogger<UserDataCache> logger)
        {
            this._cachingOptions = cachingOptions;
            this._log = logger;
        }

        public override void AddOrReplace(GroupConfig entity)
        {
            if (!_cachingOptions.CurrentValue.CacheGroupsConfig)
                return;
            base.AddOrReplace(entity);
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            Task task = AutoClearLoopAsync(cancellationToken);
            if (task.IsCompleted)
                return task;
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        private async Task AutoClearLoopAsync(CancellationToken cancellationToken = default)
        {
            if (_expirationTime < TimeSpan.Zero)
            {
                _log.LogDebug("{ServiceName} cache expiration time set to 0 or lower, cache purges disabled", this.GetType().Name);
                return;
            }

            _log.LogDebug("{ServiceName} starting auto-clear loop with rate of {ClearRate}", this.GetType().Name, _expirationTime);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_expirationTime, cancellationToken).ConfigureAwait(false);
                int removedCount = base.ClearExpired();
                _log.LogDebug("{RemovedCount} expired {ServiceName} entities removed from cache", removedCount, this.GetType().Name);
            }
        }

        protected override bool IsEntityExpired(CachedEntity<GroupConfig> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
        }
    }
}
