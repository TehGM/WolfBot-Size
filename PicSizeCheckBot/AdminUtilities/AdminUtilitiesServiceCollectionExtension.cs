using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.AdminUtilities;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AdminUtilitiesServiceCollectionExtension
    {
        public static IServiceCollection AddAdminUtilities(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IIdQueueCache, IdQueueCache>();
            services.TryAddSingleton<IUserDataStore, MongoUserDataStore>();
            services.TryAddSingleton<IUserDataCache, UserDataCache>();
            services.TryAddSingleton<IGroupConfigCache, GroupConfigCache>();
            services.AddHostedService<CacheAdminHandler>();
            services.AddHostedService<BotAdminHandler>();

            return services;
        }
    }
}
