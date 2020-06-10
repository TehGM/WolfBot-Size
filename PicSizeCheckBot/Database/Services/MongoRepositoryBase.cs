using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public abstract class MongoRepositoryBase : IDisposable
    {
        protected IMongoConnection MongoConnection { get; }
        protected IOptionsMonitor<DatabaseOptions> DatabaseOptions { get; }

        public MongoRepositoryBase(IMongoConnection databaseConnection, IOptionsMonitor<DatabaseOptions> databaseOptions)
        {
            this.DatabaseOptions = databaseOptions;
            this.MongoConnection = databaseConnection;
            this.MongoConnection.ClientChanged += OnMongoClientChanged;
        }

        protected abstract void OnMongoClientChanged(MongoClient newClient);

        public virtual void Dispose()
        {
            this.MongoConnection.ClientChanged -= OnMongoClientChanged;
        }
    }
}
