using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoGroupConfigStore : MongoRepositoryBase, IGroupConfigStore
    {
        private readonly ILogger<MongoGroupConfigStore> _log;
        private IMongoCollection<GroupConfig> _groupConfigsCollection;
        private readonly ReplaceOptions _replaceOptions;

        public MongoGroupConfigStore(IMongoConnection databaseConnection, ILogger<MongoGroupConfigStore> logger, IOptionsMonitor<DatabaseOptions> databaseOptions)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _groupConfigsCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<GroupConfig>(options.GroupConfigsCollectionName);
        }

        public async Task<GroupConfig> GetGroupConfigAsync(uint groupID, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Retrieving group config for group {GroupID} from database", groupID);
            GroupConfig result = await _groupConfigsCollection.Find(dbConfig => dbConfig.GroupID == groupID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            // if not found, return default data
            if (result == null)
            {
                _log.LogTrace("Group config for group {GroupID} not found, creating new with defaults", groupID);
                result = new GroupConfig(groupID);
            }
            return result;
        }

        public Task SetGroupConfigAsync(GroupConfig config, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Inserting group config for group {GroupID} into database", config.GroupID);
            return _groupConfigsCollection.ReplaceOneAsync(dbData => dbData.GroupID == config.GroupID, config, _replaceOptions, cancellationToken);
        }
    }
}
