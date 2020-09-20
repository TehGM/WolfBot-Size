using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CachingServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityCaching(this IServiceCollection services, IConfiguration configurationSection, Action<CachingOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IIdQueueCache, IdQueueCache>();
            services.TryAddSingleton<IUserDataCache, UserDataCache>();
            services.TryAddSingleton<IGroupConfigCache, GroupConfigCache>();
            services.TryAddSingleton<IMentionConfigCache, MentionConfigCache>();
            services.AddHostedService<CacheCleaner>();

            if (configurationSection != null)
                services.Configure<CachingOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddEntityCaching(this IServiceCollection services, Action<CachingOptions> configureOptions = null)
            => services.AddEntityCaching(null, configureOptions);
    }
}
