using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.Mentions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MentionsServiceCollectionExtensions
    {
        public static IServiceCollection AddMentions(this IServiceCollection services, IConfiguration configurationSection, Action<MentionsOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddWolfClient();
            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IMentionConfigStore, MongoMentionConfigStore>();
            services.TryAddSingleton<IMentionConfigCache, MentionConfigCache>();
            services.AddHostedService<MentionsHandler>();

            if (configurationSection != null)
                services.Configure<MentionsOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddMentions(this IServiceCollection services, Action<MentionsOptions> configureOptions = null)
            => services.AddMentions(null, configureOptions);
    }
}
