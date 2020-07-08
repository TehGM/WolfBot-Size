using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.UserNotes;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class UserNotesServiceCollectionExtensions
    {
        public static IServiceCollection AddUserNotes(this IServiceCollection services, IConfiguration configurationSection, Action<UserNotesOptions> configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddWolfClient();
            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IUserDataStore, MongoUserDataStore>();
            services.TryAddSingleton<IUserDataCache, UserDataCache>();
            services.AddHostedService<UserNotesHandler>();

            if (configurationSection != null)
                services.Configure<UserNotesOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            return services;
        }

        public static IServiceCollection AddUserNotes(this IServiceCollection services, Action<UserNotesOptions> configureOptions = null)
            => services.AddUserNotes(null, configureOptions);
    }
}
