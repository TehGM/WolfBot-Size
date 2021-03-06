﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class CacheCleaner : BackgroundService, IDisposable
    {
        private CancellationToken _hostedCancellationToken;
        private CancellationTokenSource _cts;
        private readonly IDisposable _optionsChangeHandle;

        private readonly ILogger _log;
        private readonly IOptionsMonitor<CachingOptions> _cachingOptions;
        // caches
        private readonly IUserDataCache _userDataCache;
        private readonly IGroupConfigCache _groupConfigCache;
        private readonly IIdQueueCache _idQueueCache;
        private readonly IMentionConfigCache _mentionConfigCache;
        // stores
        private readonly IUserDataStore _userDataStore;
        private readonly IIdQueueStore _idQueueStore;
        private readonly IGroupConfigStore _groupConfigStore;

        public CacheCleaner(ILogger<CacheCleaner> logger, IOptionsMonitor<CachingOptions> cachingOptions,
            IUserDataStore userDataStore, IIdQueueStore idQueueStore, IGroupConfigStore groupConfigStore,
            IUserDataCache userDataCache, IGroupConfigCache groupConfigCache, IIdQueueCache idQueueCache, IMentionConfigCache mentionConfigCache)
        {
            this._log = logger;
            this._cachingOptions = cachingOptions;
            // caches
            this._userDataCache = userDataCache;
            this._groupConfigCache = groupConfigCache;
            this._idQueueCache = idQueueCache;
            this._mentionConfigCache = mentionConfigCache;
            // stores
            this._userDataStore = userDataStore;
            this._idQueueStore = idQueueStore;
            this._groupConfigStore = groupConfigStore;

            this._optionsChangeHandle = this._cachingOptions.OnChange(_ =>
            {
                this.RestartAllLoops();
            });
        }

        private async Task AutoClearLoopAsync<TKey, TEntity>(IEntityCache<TKey, TEntity> cache, string optionsName, IBatchingStore store,
            CancellationToken cancellationToken = default) where TEntity : IEntity<TKey>
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
                // flush batch to prevent data loss
                store?.FlushBatch();
                // find and remove entities from cache
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

            _ = AutoClearLoopAsync(_userDataCache, UserDataCache.OptionName, _userDataStore, _cts.Token);
            _ = AutoClearLoopAsync(_groupConfigCache, GroupConfigCache.OptionName, _groupConfigStore, _cts.Token);
            _ = AutoClearLoopAsync(_idQueueCache, IdQueueCache.OptionName, _idQueueStore, _cts.Token);
            _ = AutoClearLoopAsync(_mentionConfigCache, MentionConfigCache.OptionName, null, _cts.Token);
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
