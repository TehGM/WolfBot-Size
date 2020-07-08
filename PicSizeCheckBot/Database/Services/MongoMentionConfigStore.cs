using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoMentionConfigStore : MongoRepositoryBase, IMentionConfigStore
    {
        // db stuff
        private IMongoCollection<MentionConfig> _mentionConfigsCollection;
        private readonly IMentionConfigCache _cache;
        // misc
        private readonly ILogger _log;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public MongoMentionConfigStore(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions, 
            ILogger<MongoGroupConfigStore> logger, IMentionConfigCache cache)
            : base(databaseConnection, databaseOptions)
        {
            this._log = logger;
            this._cache = cache;
            this.OnMongoClientChanged(base.MongoConnection.Client);
        }

        protected override void OnMongoClientChanged(MongoClient newClient)
        {
            DatabaseOptions options = base.DatabaseOptions.CurrentValue;
            _mentionConfigsCollection = newClient.GetDatabase(options.DatabaseName).GetCollection<MentionConfig>(options.MentionConfigsCollectionName);
        }


        public async Task<IEnumerable<MentionConfig>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // check cache first
                IEnumerable<MentionConfig> results = _cache.Find(_ => true);
                if (results?.Any() == true)
                {
                    _log.LogTrace("Mentions configs found in cache");
                    return results;
                }

                // get from DB
                _cache.Clear();
                _log.LogTrace("Retrieving mention configs from database");
                results = await _mentionConfigsCollection.Find(_ => true).ToListAsync().ConfigureAwait(false);

                // cache results
                if (results?.Any() == true)
                {
                    foreach (MentionConfig cnf in results)
                        _cache.AddOrReplace(cnf);
                }

                return results;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
