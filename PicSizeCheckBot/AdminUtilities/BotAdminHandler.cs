using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace TehGM.WolfBots.PicSizeCheckBot.AdminUtilities
{
    public class BotAdminHandler : IHostedService, IDisposable
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IUserDataStore _userDataStore;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;

        public BotAdminHandler(IHostedWolfClient client, IUserDataStore userDataStore,
            IOptionsMonitor<BotOptions> botOptions, ILogger<BotAdminHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._client = client;
            this._userDataStore = userDataStore;

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

                CancellationToken cancellationToken = _cts?.Token ?? default;
                if (command.StartsWith("join", StringComparison.OrdinalIgnoreCase))
                    await CmdJoinAsync(message, command, cancellationToken).ConfigureAwait(false);
                else if (command.StartsWith("leave", StringComparison.OrdinalIgnoreCase))
                    await CmdLeaveAsync(message, command, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        private async Task CmdJoinAsync(ChatMessage message, string command, CancellationToken cancellationToken = default)
        {
            if (!await CheckIfAdminAsync(message, cancellationToken).ConfigureAwait(false))
                return;

            string groupName = null;
            if (command.Length > "join".Length)
                groupName = command.Substring("join".Length).Trim();

            if (string.IsNullOrWhiteSpace(groupName))
            {
                await _client.ReplyTextAsync(message, "(n) Please provide group name.", cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                WolfGroup group = await _client.JoinGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
                await _client.ReplyTextAsync(message, $"(y) Joined [{group.Name}].", cancellationToken).ConfigureAwait(false);
            }
            catch (MessageSendingException ex)
            {
                if (ex.Response is WolfResponse response && response.ErrorCode != null)
                    await _client.ReplyTextAsync(message,
                        $"(n) Failed joining group: {response.ErrorCode.Value.GetDescription(ex.SentMessage.Command)}",
                        cancellationToken).ConfigureAwait(false);
                else
                    throw;
            }
        }

        private async Task CmdLeaveAsync(ChatMessage message, string command, CancellationToken cancellationToken = default)
        {
            if (!await CheckIfAdminAsync(message, cancellationToken).ConfigureAwait(false))
                return;

            string groupName = null;
            if (command.Length > "leave".Length)
                groupName = command.Substring("leave".Length).Trim();

            if (string.IsNullOrWhiteSpace(groupName))
            {
                if (!message.IsGroupMessage)
                {
                    await _client.ReplyTextAsync(message, "(n) Please provide group name.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                await _client.LeaveGroupAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);
                return;
            }


            try
            {
                // get group
                WolfGroup group = await _client.GetGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
                if (group == null)
                {
                    await _client.ReplyTextAsync(message, $"(n) Group \"{groupName}\" not found.", cancellationToken).ConfigureAwait(false);
                    return;
                }
                // leave it
                await _client.LeaveGroupAsync(group.ID, cancellationToken).ConfigureAwait(false);

                // send acknowledgment
                if (!message.IsGroupMessage || group.ID != message.RecipientID)
                    await _client.ReplyTextAsync(message, $"(y) Left [{group.Name}].", cancellationToken).ConfigureAwait(false);
                else
                    await _client.SendPrivateTextMessageAsync(message.SenderID.Value, $"(y) Left [{group.Name}].", cancellationToken).ConfigureAwait(false);
            }
            catch (MessageSendingException ex)
            {
                if (ex.Response is WolfResponse response && response.ErrorCode != null)
                    await _client.ReplyTextAsync(message,
                        $"/alert Failed leaving group: {response.ErrorCode.Value.GetDescription(ex.SentMessage.Command)}",
                        cancellationToken).ConfigureAwait(false);
                else
                    throw;
            }
        }

        private async Task<bool> CheckIfAdminAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            // check if user is bot admin
            UserData userData = await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            if (!userData.IsBotAdmin)
            {
                await _client.ReplyTextAsync(message, "(n) You are not permitted to do this!", cancellationToken).ConfigureAwait(false);
                return false;
            }
            return true;
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
