using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.QueuesSystem;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class QueuesSystemServiceCollectionExtensions
    {
        public static IServiceCollection AddQueuesSystem(this IServiceCollection services, IConfiguration configurationSection, Action<QueuesSystemOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IIdQueueStore, MongoIdQueuesStore>();
            services.TryAddSingleton<IIdQueueCache, IdQueueCache>();
            services.AddHostedService<QueuesSystemHandler>();

            if (configurationSection != null)
                services.Configure<QueuesSystemOptions>(configurationSection);
            services.AddSingleton<IPostConfigureOptions<QueuesSystemOptions>, ConfigureQueueSystemOptions>();
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddQueuesSystem(this IServiceCollection services, Action<QueuesSystemOptions> configureOptions = null)
            => services.AddQueuesSystem(null, configureOptions);
    }
}

