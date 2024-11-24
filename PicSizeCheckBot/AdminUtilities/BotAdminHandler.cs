using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace TehGM.WolfBots.PicSizeCheckBot.AdminUtilities
{
    [CommandsHandler]
    [Hidden]
    public class BotAdminHandler : IDisposable
    {
        private readonly IWolfClient _client;

        private readonly CancellationTokenSource _cts;
        private readonly List<CommandContext> _reconnectRequests;
        private bool _reconnecting = false;

        public BotAdminHandler(IWolfClient client)
        {
            // store all services
            this._client = client;
            this._cts = new CancellationTokenSource();

            // add client listeners
            this._client.AddMessageListener<WelcomeEvent>(this.OnLoggedIn);

            // init relog requests track
            this._reconnectRequests = new List<CommandContext>(1);
        }

        [Command("join")]
        [RequireBotAdmin]
        private async Task CmdJoinAsync(CommandContext context, [MissingError("(n) Please provide group name.")] string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                await context.ReplyTextAsync("(n) Please provide group name.", cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                WolfGroup group = await context.Client.JoinGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
                await context.ReplyTextAsync($"(y) Joined [{group.Name}].", cancellationToken).ConfigureAwait(false);
            }
            catch (MessageSendingException ex)
            {
                if (ex.Response is WolfResponse response && response.ErrorCode != null)
                    await context.ReplyTextAsync($"(n) Failed joining group: {response.ErrorCode.Value.GetDescription(ex.SentMessage.EventName)}", cancellationToken).ConfigureAwait(false);
                else
                    throw;
            }
        }

        [Command("leave")]
        [RequireBotAdmin]
        private async Task CmdLeaveAsync(CommandContext context, string groupName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                if (!context.IsGroup)
                {
                    await context.ReplyTextAsync("(n) Please provide group name.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                await context.Client.LeaveGroupAsync(context.Message.RecipientID, cancellationToken).ConfigureAwait(false);
                return;
            }


            try
            {
                // get group
                WolfGroup group = await context.Client.GetGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
                if (group == null)
                {
                    await context.ReplyTextAsync($"(n) Group \"{groupName}\" not found.", cancellationToken).ConfigureAwait(false);
                    return;
                }
                // leave it
                await context.Client.LeaveGroupAsync(group.ID, cancellationToken).ConfigureAwait(false);

                // send acknowledgment
                if (!context.IsGroup || group.ID != context.Message.RecipientID)
                    await context.ReplyTextAsync($"(y) Left [{group.Name}].", cancellationToken).ConfigureAwait(false);
                else
                    await context.Client.SendPrivateTextMessageAsync(context.Message.SenderID.Value, $"(y) Left [{group.Name}].", cancellationToken).ConfigureAwait(false);
            }
            catch (MessageSendingException ex)
            {
                if (ex.Response is WolfResponse response && response.ErrorCode != null)
                    await context.ReplyTextAsync($"/alert Failed leaving group: {response.ErrorCode.Value.GetDescription(ex.SentMessage.EventName)}", cancellationToken).ConfigureAwait(false);
                else
                    throw;
            }
        }

        [Command("reconnect")]
        [RequireBotAdmin]
        private async Task CmdReconnectAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            if (_reconnecting)
                return;

            await context.ReplyTextAsync("/me I am now going to reconnect, hang on.", cancellationToken).ConfigureAwait(false);
            _reconnecting = true;
            _reconnectRequests.Add(context);
            await context.Client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            await context.Client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        private async void OnLoggedIn(WelcomeEvent message)
        {
            try
            {
                if (!_reconnecting || _reconnectRequests.Count == 0)
                    return;

                for (int i = 0; i < _reconnectRequests.Count; i++)
                    await _reconnectRequests[i].ReplyTextAsync("/me I am back online.", _cts?.Token ?? default).ConfigureAwait(false);
            }
            finally
            {
                _reconnectRequests.Clear();
                _reconnecting = false;
            }
        }

        public void Dispose()
        {
            this._client?.RemoveMessageListener<WelcomeEvent>(this.OnLoggedIn);
            try { this._cts?.Cancel(); } catch { }
            try { this._cts?.Dispose(); } catch { }
        }
    }
}
