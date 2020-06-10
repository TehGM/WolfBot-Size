using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoGroupConfigStore : MongoRepositoryBase, IGroupConfigStore, IDisposable
    {
        // db stuff
        private IMongoCollection<GroupConfig> _groupConfigsCollection;
        private MongoDelayedBatchInserter<uint, GroupConfig> _batchInserter;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IGroupConfigCache _cache;
        // event registrations
        private readonly IDisposable _hostStoppingRegistration;
        private readonly IDisposable _configChangeRegistration;
        // misc
        private readonly ILogger<MongoGroupConfigStore> _log;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public MongoGroupConfigStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions, IHostApplicationLifetime hostLifetime,
            ILogger<MongoGroupConfigStore> logger, IGroupConfigCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
            this.RecreateBatchInserter();
            this.OnMongoClientChanged(base.MongoConnection.Client);

            this._hostStoppingRegistration = hostLifetime.ApplicationStopping.Register(_batchInserter.Flush);
            this._configChangeRegistration = base.DatabaseOptions.OnChange(_ => RecreateBatchInserter());
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _groupConfigsCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<GroupConfig>(options.GroupConfigsCollectionName);
            _batchInserter?.UpdateCollection(_groupConfigsCollection);
        }

        private void RecreateBatchInserter()
        {
            // validate delay is valid
            TimeSpan delay = base.DatabaseOptions.CurrentValue.GroupConfigsBatchDelay;
            if (delay <= TimeSpan.Zero)
                throw new ArgumentException("Batching delay must be greater than 0", nameof(base.DatabaseOptions.CurrentValue.GroupConfigsBatchDelay));

            // flush existing inserter to not lose any changes
            if (_batchInserter != null)
                _batchInserter.Flush();
            _log?.LogDebug("Creating batch insertert for item type {ItemType} with delay of {Delay}", typeof(GroupConfig).Name, delay);
            _batchInserter = new MongoDelayedBatchInserter<uint, GroupConfig>(delay, _log);
            _batchInserter.UpdateCollection(_groupConfigsCollection);
        }

        public async Task<GroupConfig> GetGroupConfigAsync(uint groupID, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // check cache first
                GroupConfig result = _cache.Get(groupID);
                if (result != null)
                {
                    _log.LogTrace("Group config for group {GroupID} found in cache", groupID);
                    return result;
                }

                // get from DB
                _log.LogTrace("Retrieving group config for group {GroupID} from database", groupID);
                result = await _groupConfigsCollection.Find(dbConfig => dbConfig.ID == groupID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                // if not found, return default data
                if (result == null)
                {
                    _log.LogTrace("Group config for group {GroupID} not found, creating new with defaults", groupID);
                    result = new GroupConfig(groupID);
                }

                _cache.AddOrReplace(result);
                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SetGroupConfigAsync(GroupConfig config, bool instant, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _log.LogTrace("Inserting group config for group {GroupID} into database", config.ID);
                _cache.AddOrReplace(config);
                Expression<Func<GroupConfig, bool>> selector = dbConfig => dbConfig.ID == config.ID;
                if (instant)
                    await _groupConfigsCollection.ReplaceOneAsync(selector, config, _replaceOptions, cancellationToken).ConfigureAwait(false);
                else
                    await _batchInserter.BatchAsync(config.ID, new MongoDelayedInsert<GroupConfig>(selector, config, _replaceOptions)).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override void Dispose()
        {
            this._configChangeRegistration?.Dispose();
            this._hostStoppingRegistration?.Dispose();
            this._batchInserter?.Flush();
            this._batchInserter?.Dispose();
            base.Dispose();
        }
    }
}
