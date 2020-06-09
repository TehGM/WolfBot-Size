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
        private readonly ILogger<MongoGroupConfigStore> _log;
        private IMongoCollection<GroupConfig> _groupConfigsCollection;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IGroupConfigCache _cache;
        private readonly MongoDelayedBatchInserter<uint, GroupConfig> _batchInserter;
        private readonly IDisposable _hostStoppingRegistration;

        public MongoGroupConfigStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions, IHostApplicationLifetime hostLifetime,
            ILogger<MongoGroupConfigStore> logger, IGroupConfigCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
            this._batchInserter = new MongoDelayedBatchInserter<uint, GroupConfig>(TimeSpan.FromMinutes(10));

            this._hostStoppingRegistration = hostLifetime.ApplicationStopping.Register(_batchInserter.Flush);
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _groupConfigsCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<GroupConfig>(options.GroupConfigsCollectionName);
        }

        public async Task<GroupConfig> GetGroupConfigAsync(uint groupID, CancellationToken cancellationToken = default)
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

        public Task SetGroupConfigAsync(GroupConfig config, bool instant, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Inserting group config for group {GroupID} into database", config.ID);
            _cache.AddOrReplace(config);
            Expression<Func<GroupConfig, bool>> selector = dbConfig => dbConfig.ID == config.ID;
            if (instant)
                return _groupConfigsCollection.ReplaceOneAsync(selector, config, _replaceOptions, cancellationToken);
            else
                return _batchInserter.BatchAsync(config.ID, new MongoDelayedInsert<GroupConfig>(selector, config, _replaceOptions));
        }

        public override void Dispose()
        {
            this._hostStoppingRegistration?.Dispose();
            this._batchInserter?.Flush();
            this._batchInserter?.Dispose();
            base.Dispose();
        }
    }
}
