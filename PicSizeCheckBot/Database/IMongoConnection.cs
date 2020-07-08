using MongoDB.Driver;
using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public interface IMongoConnection
    {
        MongoClient Client { get; }
        event Action<MongoClient> ClientChanged;
        void RegisterClassMap<T>();
    }
}
