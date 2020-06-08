using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoUserDataStore : MongoRepositoryBase, IUserDataStore
    {
        private readonly ILogger<MongoUserDataStore> _log;
        private IMongoCollection<UserData> _usersDataCollection;
        private readonly ReplaceOptions _replaceOptions;
        private readonly IUserDataCache _cache;

        public MongoUserDataStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions,
            ILogger<MongoUserDataStore> logger, IUserDataCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this._replaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _usersDataCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<UserData>(options.UsersDataCollectionName);
        }

        public async Task<UserData> GetUserDataAsync(uint userID, CancellationToken cancellationToken = default)
        {
            // check cache first
            UserData result = _cache.GetByKey(userID);
            if (result != null)
            {
                _log.LogTrace("User data for user {UserID} found in cache", userID);
                return result;
            }

            // get from DB
            _log.LogTrace("Retrieving user data for user {UserID} from database", userID);
            result = await _usersDataCollection.Find(dbData => dbData.UserID == userID).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            // if not found, return default data
            if (result == null)
            {
                _log.LogTrace("User data for user {UserID} not found, creating new with defaults", userID);
                result = new UserData(userID);
            }

            _cache.AddOrReplace(result);
            return result;
        }

        public Task SetUserDataAsync(UserData data, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Inserting user data for user {UserID} into database", data.UserID);
            _cache.AddOrReplace(data);
            return _usersDataCollection.ReplaceOneAsync(dbData => dbData.UserID == data.UserID, data, _replaceOptions, cancellationToken);
        }
    }
}
