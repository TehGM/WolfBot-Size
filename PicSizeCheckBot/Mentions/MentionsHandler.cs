using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;
using TehGM.Wolfringo.Utilities;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionsHandler : IHostedService, IDisposable
    {
        private readonly IWolfClient _client;
        private readonly IOptionsMonitor<MentionsOptions> _mentionsOptions;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IMentionConfigStore _mentionConfigStore;
        private readonly ILogger _log;
        private readonly IHostEnvironment _environment;

        private CancellationTokenSource _cts;

        public MentionsHandler(IWolfClient client, IMentionConfigStore mentionConfigStore, IOptionsMonitor<BotOptions> botOptions, IOptionsMonitor<MentionsOptions> mentionsOptions, ILogger<MentionsHandler> logger, IHostEnvironment environment)
        {
            // store all services
            this._log = logger;
            this._mentionsOptions = mentionsOptions;
            this._botOptions = botOptions;
            this._client = client;
            this._mentionConfigStore = mentionConfigStore;
            this._environment = environment;

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            using IDisposable logScope = message.BeginLogScope(_log);

            // run only in prod, test group or owner PM
            if (!_environment.IsProduction() &&
                !((message.IsGroupMessage && message.RecipientID == _botOptions.CurrentValue.TestGroupID) ||
                (message.IsPrivateMessage && message.SenderID == _botOptions.CurrentValue.OwnerID)))
                return;

            try
            {
                // only work in group text messages
                if (!message.IsText || !message.IsGroupMessage)
                    return;

                // quit early if group or user is ignored
                if (_mentionsOptions.CurrentValue.IgnoredGroups?.Contains(message.RecipientID) == true)
                    return;
                if (_mentionsOptions.CurrentValue.IgnoredUsers?.Contains(message.SenderID.Value) == true)
                    return;

                CancellationToken cancellationToken = _cts?.Token ?? default;
                IEnumerable<MentionConfig> allMentions = await _mentionConfigStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
                if (allMentions?.Any() == true)
                {
                    foreach (MentionConfig mentionConfig in allMentions)
                    {
                        if (!mentionConfig.Patterns.Any(pattern => pattern.Regex.IsMatch(message.Text)))
                            continue;
                        if (mentionConfig.IgnoreSelf && mentionConfig.ID == message.SenderID.Value)
                            continue;

                        string text = await BuildMentionMessage(message, mentionConfig, cancellationToken).ConfigureAwait(false);
                        await _client.SendPrivateTextMessageAsync(mentionConfig.ID, text, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (MessageSendingException ex) when (ex.SentMessage is ChatMessage && ex.Response is WolfResponse response && response.ErrorCode == WolfErrorCode.LoginIncorrectOrCannotSendToGroup) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        private async Task<string> BuildMentionMessage(ChatMessage message, MentionConfig config, CancellationToken cancellationToken = default)
        {
            StringBuilder builder = new StringBuilder(config.MessageTemplate ?? _mentionsOptions.CurrentValue.DefaultMessageTemplate);
            WolfUser user = await _client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            WolfGroup group = await _client.GetGroupAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);

            // metadata
            builder.Replace("{{UserID}}", user.ID.ToString());
            builder.Replace("{{UserName}}", user.Nickname);
            builder.Replace("{{GroupID}}", group.ID.ToString());
            builder.Replace("{{GroupName}}", group.Name);

            // trim text if too long
            string txt = message.Text;
            if (txt.Length > _mentionsOptions.CurrentValue.MaxTextLength)
                txt = txt.Remove(_mentionsOptions.CurrentValue.MaxTextLength) + "...";
            builder.Replace("{{Message}}", txt);

            // return results
            return builder.ToString();
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
            try { this._cts?.Cancel(); } catch { }
            try { this._cts?.Dispose(); } catch { }
        }
    }
}