﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;

namespace TehGM.WolfBots.PicSizeCheckBot.AdminUtilities
{
    [CommandsHandler]
    [Hidden]
    public class CacheAdminHandler
    {
        private readonly ILogger _log;
        // caches
        private readonly IUserDataCache _userDataCache;
        private readonly IGroupConfigCache _groupConfigCache;
        private readonly IIdQueueCache _idQueueCache;
        private readonly IMentionConfigCache _mentionConfigCache;
        // stores
        private readonly IUserDataStore _userDataStore;
        private readonly IIdQueueStore _idQueueStore;
        private readonly IGroupConfigStore _groupConfigStore;



        public CacheAdminHandler( IUserDataStore userDataStore, IIdQueueStore idQueueStore, IGroupConfigStore groupConfigStore, IUserDataCache userDataCache, IGroupConfigCache groupConfigCache, IIdQueueCache idQueueCache, IMentionConfigCache mentionConfigCache, ILogger<CacheAdminHandler> logger)
        {
            // store all services
            this._log = logger;
            // caches
            this._groupConfigCache = groupConfigCache;
            this._userDataCache = userDataCache;
            this._idQueueCache = idQueueCache;
            this._mentionConfigCache = mentionConfigCache;
            // stores
            this._userDataStore = userDataStore;
            this._idQueueStore = idQueueStore;
            this._groupConfigStore = groupConfigStore;
        }


        [Command("cache clear")]
        [RequireBotAdmin]
        public async Task CmdCacheClear(CommandContext context, CancellationToken cancellationToken = default)
        {
            // flush all batches first to prevent data loss
            this._groupConfigStore.FlushBatch();
            this._idQueueStore.FlushBatch();
            this._userDataStore.FlushBatch();

            // get current counts for reporting
            int userDataCacheCount = _userDataCache.CachedCount;
            int groupConfigCacheCount = _groupConfigCache.CachedCount;
            int idQueueCacheCount = _idQueueCache.CachedCount;
            int mentionConfigCacheCount = _mentionConfigCache.CachedCount;

            // clear caches
            this._userDataCache.Clear();
            this._groupConfigCache.Clear();
            this._idQueueCache.Clear();
            this._mentionConfigCache.Clear();

            // reply to user
            await context.ReplyTextAsync("(y) Database caches cleared:\r\n" +
                $"{nameof(IUserDataCache)}: {userDataCacheCount}\r\n" +
                $"{nameof(IGroupConfigCache)}: {groupConfigCacheCount}\r\n" +
                $"{nameof(IIdQueueCache)}: {idQueueCacheCount}\r\n" +
                $"{nameof(IMentionConfigCache)}: {mentionConfigCacheCount}",
                cancellationToken).ConfigureAwait(false);

            // log the change
            WolfUser user = await context.Client.GetUserAsync(context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            using IDisposable clearedLogScope = _log.BeginScope(new Dictionary<string, object>()
                {
                    { "UserDataCacheCount", userDataCacheCount },
                    { "GroupConfigCacheCount", groupConfigCacheCount },
                    { "IdQueueCacheCount", idQueueCacheCount },
                    { "MentionConfigCacheCount", mentionConfigCacheCount }
                });
            this._log.LogInformation("All database caches cleared by {UserID} ({UserNickname})", user.ID, user.Nickname);
        }
    }
}
