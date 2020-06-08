using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public class CacheCleaner : BackgroundService, IDisposable
    {
        private CancellationToken _hostedCancellationToken;
        private CancellationTokenSource _cts;
        private readonly IDisposable _optionsChangeHandle;

        private readonly ILogger _log;
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;
        private readonly IUserDataCache _userDataCache;
        private readonly IGroupConfigCache _groupConfigCache;

        public CacheCleaner(ILogger<CacheCleaner> logger, IOptionsMonitor<CachingOptions> cachingOptions,
            IUserDataCache userDataCache, IGroupConfigCache groupConfigCache)
        {
            this._log = logger;
            this._cachingOptions = cachingOptions;
            this._userDataCache = userDataCache;
            this._groupConfigCache = groupConfigCache;

            this._optionsChangeHandle = this._cachingOptions.OnChange(_ =>
            {
                this.RestartAllLoops();
            });
        }

        private async Task AutoClearLoopAsync<TKey, TEntity>(IEntityCache<TKey, TEntity> cache, string optionsName, CancellationToken cancellationToken = default) where TEntity : IEntity<TKey>
        {
            CachingOptions options = _cachingOptions.Get(optionsName);

            if (options.Lifetime < TimeSpan.Zero)
            {
                _log.LogDebug("{ServiceName} cache lifetime set to 0 or lower, cache purges disabled", cache.GetType().Name);
                return;
            }

            _log.LogDebug("{ServiceName} starting cache auto-clear loop with rate of {ClearRate}", cache.GetType().Name, options.Lifetime);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(options.Lifetime, cancellationToken).ConfigureAwait(false);
                IEnumerable<TEntity> expired = cache.Find(e => e.IsExpired(options.Lifetime));
                foreach (TEntity entity in expired)
                    cache.Remove(entity.ID);
                _log.LogDebug("{RemovedCount} expired {ServiceName} entities removed from cache", expired.Count(), cache.GetType().Name);
            }
        }

        private void RestartAllLoops()
        {
            this._cts?.Cancel();
            this._cts?.Dispose();
            this._cts = CancellationTokenSource.CreateLinkedTokenSource(_hostedCancellationToken);

            _ = AutoClearLoopAsync(_userDataCache, "UserData", _cts.Token);
            _ = AutoClearLoopAsync(_groupConfigCache, "GroupConfig", _cts.Token);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this._hostedCancellationToken = stoppingToken;
            this.RestartAllLoops();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this._optionsChangeHandle?.Dispose();
            this._cts?.Cancel();
            this._cts?.Dispose();
            base.Dispose();
        }
    }
}
