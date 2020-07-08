using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Database.Conventions;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoConnection : IMongoConnection
    {
        public MongoClient Client { get; private set; }
        public event Action<MongoClient> ClientChanged;

        private readonly IOptionsMonitor<DatabaseOptions> _databaseOptions;
        private readonly ILogger<MongoConnection> _log;

        public MongoConnection(IOptionsMonitor<DatabaseOptions> databaseOptions, ILogger<MongoConnection> logger)
        {
            this._log = logger;
            this._databaseOptions = databaseOptions;

            FixMongoMapping();

            _databaseOptions.OnChange(_ =>
            {
                InitializeConnection();
                this.ClientChanged?.Invoke(this.Client);
            });
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            _log.LogTrace("Establishing connection to MongoDB...");
            this.Client = new MongoClient(_databaseOptions.CurrentValue.ConnectionString);
        }

        public static void FixMongoMapping()
        {
            // init mongodb mapping conventions
            //BsonSerializer.RegisterDiscriminatorConvention(typeof(object), new ClassNameDiscriminatorConvention());
            ConventionPack conventionPack = new ConventionPack();
            conventionPack.Add(new MapReadOnlyPropertiesConvention());
            conventionPack.Add(new GuidAsStringRepresentationConvention());
            //conventionPack.AddClassMapConvention("AlwaysApplyDiscriminator", map => map.SetDiscriminatorIsRequired(true));
            ConventionRegistry.Register("Conventions", conventionPack, _ => true);
        }

        public void RegisterClassMap<T>()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
                BsonClassMap.RegisterClassMap<T>();
        }
    }
}
