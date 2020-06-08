using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoIdQueuesStore : MongoRepositoryBase, IIdQueueStore
    {
        private readonly ILogger<MongoIdQueuesStore> _log;
        private IMongoCollection<IdQueue> _usersDataCollection;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IIdQueueCache _cache;

        public MongoIdQueuesStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions,
            ILogger<MongoIdQueuesStore> logger, IIdQueueCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _usersDataCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<IdQueue>(options.IdQueuesCollectionName);
        }

        public async Task<IdQueue> GetIdQueueAsync(string name, CancellationToken cancellationToken = default)
        {
            // check cache first
            IdQueue result = _cache.Get(name);
            if (result != null)
            {
                _log.LogTrace("IDs queue {QueueName} found in cache", name);
                return result;
            }

            // get from DB
            _log.LogTrace("Retrieving IDs queue {QueueName} from database", name);
            result = await _usersDataCollection.Find(dbQueue => dbQueue.Name == name).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (result != null)
                _cache.AddOrReplace(result);
            else
                _log.LogTrace("IDs queue {QueueName} not found", name);

            return result;
        }

        public async Task<IdQueue> GetIdQueueByOwnerAsync(uint ownerID, CancellationToken cancellationToken = default)
        {
            // check cache first
            IdQueue result = _cache.Find(cachedQueue => cachedQueue.Entity.OwnerID == ownerID).FirstOrDefault();
            if (result != null)
            {
                _log.LogTrace("IDs queue owned by user {UserID} found in cache", ownerID);
                return result;
            }

            // get from DB
            _log.LogTrace("Retrieving IDs queue owned by user {UserID} from database", ownerID);
            result = await _usersDataCollection.Find(dbQueue => dbQueue.OwnerID == ownerID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (result != null)
                _cache.AddOrReplace(result);
            else
                _log.LogTrace("IDs queue owned by user {UserID} not found", ownerID);

            return result;
        }

        public Task SetGroupConfigAsync(IdQueue queue, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Inserting IDs queue {QueueName} into database", queue.Name);
            _cache.AddOrReplace(queue);
            return _usersDataCollection.ReplaceOneAsync(dbQueue => dbQueue.Name == queue.Name, queue, _replaceOptions, cancellationToken);
        }
    }
}
