using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoIdQueuesStore : MongoRepositoryBase, IIdQueueStore, IDisposable
    {
        // db stuff
        private IMongoCollection<IdQueue> _idQueuesCollection;
        private MongoDelayedBatchInserter<Guid, IdQueue> _batchInserter;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IIdQueueCache _cache;
        // event registrations
        private readonly IDisposable _hostStoppingRegistration;
        private readonly IDisposable _configChangeRegistration;
        // misc
        private readonly ILogger<MongoIdQueuesStore> _log;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public MongoIdQueuesStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions, IHostApplicationLifetime hostLifetime,
            ILogger<MongoIdQueuesStore> logger, IIdQueueCache cache)
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
            _idQueuesCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<IdQueue>(options.IdQueuesCollectionName);
            _batchInserter?.UpdateCollection(_idQueuesCollection);
        }

        private void RecreateBatchInserter()
        {
            // validate delay is valid
            TimeSpan delay = base.DatabaseOptions.CurrentValue.IdQueueBatchDelay;
            if (delay <= TimeSpan.Zero)
                throw new ArgumentException("Batching delay must be greater than 0", nameof(base.DatabaseOptions.CurrentValue.IdQueueBatchDelay));

            // flush existing inserter to not lose any changes
            if (_batchInserter != null)
                _batchInserter.Flush();
            _batchInserter = new MongoDelayedBatchInserter<Guid, IdQueue>(delay);
            _batchInserter.UpdateCollection(_idQueuesCollection);
        }

        public async Task<IdQueue> GetIdQueueByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // check cache first
                IdQueue result = _cache.Find(cachedQueue => cachedQueue.Entity.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (result != null)
                {
                    _log.LogTrace("IDs queue {QueueName} found in cache", name);
                    return result;
                }

                // get from DB
                _log.LogTrace("Retrieving IDs queue {QueueName} from database", name);
                result = await _idQueuesCollection.Find(dbQueue => dbQueue.Name.ToLowerInvariant() == name.ToLowerInvariant()).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (result != null)
                    _cache.AddOrReplace(result);
                else
                    _log.LogTrace("IDs queue {QueueName} not found", name);

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IdQueue> GetIdQueueByOwnerAsync(uint ownerID, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
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
                result = await _idQueuesCollection.Find(dbQueue => dbQueue.OwnerID == ownerID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (result != null)
                    _cache.AddOrReplace(result);
                else
                    _log.LogTrace("IDs queue owned by user {UserID} not found", ownerID);

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SetIdQueueAsync(IdQueue queue, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _log.LogTrace("Inserting IDs queue {QueueName} into database", queue.Name);
                _cache.AddOrReplace(queue);
                await _batchInserter.BatchAsync(queue.ID, new MongoDelayedInsert<IdQueue>(dbQueue => dbQueue.ID == queue.ID, queue, _replaceOptions)).ConfigureAwait(false);
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
