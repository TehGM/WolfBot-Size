using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.NextGameUtility;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class NextGameServiceCollectionExtensions
    {
        public static IServiceCollection AddNextGameUtility(this IServiceCollection services, IConfiguration configurationSection, Action<NextGameOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IGroupConfigStore, MongoGroupConfigStore>();
            services.TryAddSingleton<IGroupConfigCache, GroupConfigCache>();

            if (configurationSection != null)
                services.Configure<NextGameOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddNextGameUtility(this IServiceCollection services, Action<NextGameOptions> configureOptions = null)
            => services.AddNextGameUtility(null, configureOptions);
    }
}
