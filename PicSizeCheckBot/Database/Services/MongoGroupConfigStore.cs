using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoGroupConfigStore : MongoRepositoryBase, IGroupConfigStore
    {
        private readonly ILogger<MongoGroupConfigStore> _log;
        private IMongoCollection<GroupConfig> _groupConfigsCollection;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IGroupConfigCache _cache;

        public MongoGroupConfigStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions,
            ILogger<MongoGroupConfigStore> logger, IGroupConfigCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
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

        public Task SetGroupConfigAsync(GroupConfig config, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Inserting group config for group {GroupID} into database", config.ID);
            _cache.AddOrReplace(config);
            return _groupConfigsCollection.ReplaceOneAsync(dbData => dbData.ID == config.ID, config, _replaceOptions, cancellationToken);
        }
    }
}
