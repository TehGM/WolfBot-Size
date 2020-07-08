using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.SizeChecking;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SizeCheckingServiceCollectionExtensions
    {
        public static IServiceCollection AddSizeChecking(this IServiceCollection services, IConfiguration configurationSection, Action<SizeCheckingOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IUserDataStore, MongoUserDataStore>();
            services.TryAddSingleton<IUserDataCache, UserDataCache>();
            services.TryAddSingleton<IGroupConfigStore, MongoGroupConfigStore>();
            services.TryAddSingleton<IGroupConfigCache, GroupConfigCache>();
            services.AddHostedService<SizeCheckingHandler>();

            if (configurationSection != null)
                services.Configure<SizeCheckingOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddSizeChecking(this IServiceCollection services, Action<SizeCheckingOptions> configureOptions = null)
            => services.AddSizeChecking(null, configureOptions);
    }
}
