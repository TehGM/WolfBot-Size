using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using TehGM.WolfBots.PicSizeCheckBot;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Database.Services;
using TehGM.WolfBots.PicSizeCheckBot.Mentions;
using System.Linq;
using TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MentionsServiceCollectionExtensions
    {
        public static IServiceCollection AddMentions(this IServiceCollection services, IConfiguration configurationSection, Action<MentionsOptions> configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddWolfClient();
            services.TryAddSingleton<IMongoConnection, MongoConnection>();
            services.TryAddSingleton<IMentionConfigStore, MongoMentionConfigStore>();
            services.TryAddSingleton<IMentionConfigCache, MentionConfigCache>();
            services.AddHostedService<MentionsHandler>();

            if (configurationSection != null)
                services.Configure<MentionsOptions>(configurationSection);
            if (configureOptions != null)
                services.Configure(configureOptions);

            MapMentionFilterTypes();

            return services;
        }

        public static IServiceCollection AddMentions(this IServiceCollection services, Action<MentionsOptions> configureOptions = null)
            => services.AddMentions(null, configureOptions);

        private static void MapMentionFilterTypes()
        {
            IEnumerable<Type> conditionTypes = typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(IMentionFilter).IsAssignableFrom(t)
                    && !t.IsAbstract
                    && !t.IsGenericType
                    && !Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)))
                .ToArray();

            foreach (Type type in conditionTypes)
            {
                if (BsonClassMap.IsClassMapRegistered(type))
                    continue;

                string discriminator = GetDiscriminator(type);
                BsonClassMap map = new BsonClassMap(type);
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.SetDiscriminatorIsRequired(true);
                map.SetDiscriminator(discriminator);
                BsonClassMap.RegisterClassMap(map);
            }

            string GetDiscriminator(Type type)
            {
                string discriminator = type.GetCustomAttribute<BsonDiscriminatorAttribute>()?.Discriminator;
                if (discriminator != null)
                    return discriminator;

                return type.FullName;
            }
        }
    }
}
