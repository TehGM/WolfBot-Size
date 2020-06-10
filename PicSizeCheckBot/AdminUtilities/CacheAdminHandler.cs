using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.AdminUtilities
{
    public class CacheAdminHandler : IHostedService, IDisposable
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IUserDataCache _userDataCache;
        private readonly IGroupConfigCache _groupConfigCache;
        private readonly IIdQueueCache _idQueueCache;
        private readonly IUserDataStore _userDataStore;
        private readonly IMentionConfigCache _mentionConfigCache;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;

        public CacheAdminHandler(IHostedWolfClient client, IUserDataStore userDataStore,
            IUserDataCache userDataCache, IGroupConfigCache groupConfigCache, IIdQueueCache idQueueCache, IMentionConfigCache mentionConfigCache,
            IOptionsMonitor<BotOptions> botOptions, ILogger<CacheAdminHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._client = client;
            this._userDataStore = userDataStore;
            this._groupConfigCache = groupConfigCache;
            this._userDataCache = userDataCache;
            this._idQueueCache = idQueueCache;
            this._mentionConfigCache = mentionConfigCache;

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            using IDisposable logScope = message.BeginLogScope(_log);

            try
            {
                // check if this is a correct command
                if (!message.IsText)
                    return;
                if (!message.TryGetCommandValue(_botOptions.CurrentValue, out string command))
                    return;
                if (!command.StartsWith("cache clear", StringComparison.OrdinalIgnoreCase))
                    return;

                CancellationToken cancellationToken = _cts?.Token ?? default;

                // check if user is bot admin
                UserData userData = await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                if (!userData.IsBotAdmin)
                {
                    await _client.ReplyTextAsync(message, "(n) You are not permitted to do this!", cancellationToken).ConfigureAwait(false);
                    return;
                }

                // get current counts for reporting
                int userDataCacheCount = _userDataCache.CachedCount;
                int groupConfigCacheCount = _groupConfigCache.CachedCount;
                int idQueueCacheCount = _idQueueCache.CachedCount;
                int mentionConfigCacheCount = _mentionConfigCache.CachedCount;

                // clear caches
                _userDataCache.Clear();
                _groupConfigCache.Clear();
                _idQueueCache.Clear();
                _mentionConfigCache.Clear();

                // reply to user
                await _client.ReplyTextAsync(message, "(y) Database caches cleared:\r\n" +
                    $"{nameof(IUserDataCache)}: {userDataCacheCount}\r\n" +
                    $"{nameof(IGroupConfigCache)}: {groupConfigCacheCount}\r\n" +
                    $"{nameof(IIdQueueCache)}: {idQueueCacheCount}\r\n" +
                    $"{nameof(IMentionConfigCache)}: {mentionConfigCacheCount}",
                    cancellationToken).ConfigureAwait(false);

                // log the change
                WolfUser user = await _client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                using IDisposable clearedLogScope = _log.BeginScope(new Dictionary<string, object>()
                {
                    { "UserDataCacheCount", userDataCacheCount },
                    { "GroupConfigCacheCount", groupConfigCacheCount },
                    { "IdQueueCacheCount", idQueueCacheCount },
                    { "MentionConfigCacheCount", mentionConfigCacheCount }
                });
                _log.LogInformation("All database caches cleared by {UserID} ({UserNickname})", user.ID, user.Nickname);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        // Implementing IHostedService ensures this class is created on start
        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return Task.CompletedTask;
        }
        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            this.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this._client?.RemoveMessageListener<ChatMessage>(OnChatMessage);
            this._cts?.Cancel();
            this._cts?.Dispose();
            this._cts = null;
        }
    }
}
