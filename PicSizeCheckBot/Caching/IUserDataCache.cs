using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IUserDataCache : IEntityCache<uint, UserData> { }
    public class UserDataCache : EntityCache<uint, UserData>, IUserDataCache, IHostedService
    {
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;
        private readonly ILogger _log;
        private TimeSpan _expirationTime => _cachingOptions.CurrentValue.UserDataCacheLifetime;

        public UserDataCache(IOptionsMonitor<CachingOptions> cachingOptions, ILogger<UserDataCache> logger)
        {
            this._cachingOptions = cachingOptions;
            this._log = logger;
        }

        public override void AddOrReplace(uint key, UserData entity)
        {
            if (!_cachingOptions.CurrentValue.CacheUserData)
                return;
            base.AddOrReplace(key, entity);
        }

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

        protected override bool IsEntityExpired(CachedEntity<UserData> entity)
        {
            if (_expirationTime < TimeSpan.Zero)
                return false;
            return entity.CachingTimeUtc + _expirationTime < DateTime.UtcNow;
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
    }
}
