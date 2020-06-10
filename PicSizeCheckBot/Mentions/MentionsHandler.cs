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
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionsHandler : IHostedService, IDisposable
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<MentionsOptions> _mentionsOptions;
        private readonly IMentionConfigStore _mentionConfigStore;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;

        public MentionsHandler(IHostedWolfClient client, IMentionConfigStore mentionConfigStore,
            IOptionsMonitor<MentionsOptions> mentionsOptions, ILogger<MentionsHandler> logger)
        {
            // store all services
            this._log = logger;
            this._mentionsOptions = mentionsOptions;
            this._client = client;
            this._mentionConfigStore = mentionConfigStore;

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            using IDisposable logScope = message.BeginLogScope(_log);

            try
            {
                // only work in group text messages
                if (!message.IsText || !message.IsGroupMessage)
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
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        private async Task<string> BuildMentionMessage(ChatMessage message, MentionConfig config, CancellationToken cancellationToken = default)
        {
            StringBuilder builder = new StringBuilder(config.MessageTemplate ?? _mentionsOptions.CurrentValue.DefaultMessageTemplate);
            WolfUser user = await _client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            WolfGroup group = await _client.GetGroupAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);
            builder.Replace("{{UserID}}", user.ID.ToString());
            builder.Replace("{{UserName}}", user.Nickname);
            builder.Replace("{{GroupID}}", group.ID.ToString());
            builder.Replace("{{GroupName}}", group.Name);
            builder.Replace("{{Message}}", message.Text);
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
            this._cts?.Cancel();
            this._cts?.Dispose();
            this._cts = null;
        }
    }
}