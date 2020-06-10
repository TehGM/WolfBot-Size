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
    public class MongoUserDataStore : MongoRepositoryBase, IUserDataStore, IDisposable
    {
        // db stuff
        private IMongoCollection<UserData> _usersDataCollection;
        private MongoDelayedBatchInserter<uint, UserData> _batchInserter;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IUserDataCache _cache;
        // event registrations
        private readonly IDisposable _hostStoppingRegistration;
        private readonly IDisposable _configChangeRegistration;
        // misc
        private readonly ILogger<MongoUserDataStore> _log;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public MongoUserDataStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions,
            IHostApplicationLifetime hostLifetime, ILogger<MongoUserDataStore> logger, IUserDataCache cache)
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
            _usersDataCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<UserData>(options.UsersDataCollectionName);
            _batchInserter?.UpdateCollection(_usersDataCollection);
        }

        private void RecreateBatchInserter()
        {
            // validate delay is valid
            TimeSpan delay = base.DatabaseOptions.CurrentValue.UserDataBatchDelay;
            if (delay <= TimeSpan.Zero)
                throw new ArgumentException("Batching delay must be greater than 0", nameof(base.DatabaseOptions.CurrentValue.GroupConfigsBatchDelay));

            // flush existing inserter to not lose any changes
            if (_batchInserter != null)
                _batchInserter.Flush();
            _batchInserter = new MongoDelayedBatchInserter<uint, UserData>(delay, _log);
            _batchInserter.UpdateCollection(_usersDataCollection);
        }

        public async Task<UserData> GetUserDataAsync(uint userID, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // check cache first
                UserData result = _cache.Get(userID);
                if (result != null)
                {
                    _log.LogTrace("User data for user {UserID} found in cache", userID);
                    return result;
                }

                // get from DB
                _log.LogTrace("Retrieving user data for user {UserID} from database", userID);
                result = await _usersDataCollection.Find(dbData => dbData.ID == userID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                // if not found, return default data
                if (result == null)
                {
                    _log.LogTrace("User data for user {UserID} not found, creating new with defaults", userID);
                    result = new UserData(userID);
                }

                _cache.AddOrReplace(result);
                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SetUserDataAsync(UserData data, bool instant, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _log.LogTrace("Inserting user data for user {UserID} into database", data.ID);
                _cache.AddOrReplace(data);
                Expression<Func<UserData, bool>> selector = dbData => dbData.ID == data.ID;
                if (instant)
                    await _usersDataCollection.ReplaceOneAsync(selector, data, _replaceOptions, cancellationToken).ConfigureAwait(false);
                else
                    await _batchInserter.BatchAsync(data.ID, new MongoDelayedInsert<UserData>(selector, data, _replaceOptions), cancellationToken).ConfigureAwait(false);
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
