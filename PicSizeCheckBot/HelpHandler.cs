using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public class HelpHandler : IHostedService, IDisposable
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;

        public HelpHandler(IHostedWolfClient client, IOptionsMonitor<BotOptions> botOptions, ILogger<HelpHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._client = client;

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
                if (!command.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                    return;

                CancellationToken cancellationToken = _cts?.Token ?? default;

                WolfUser owner = await _client.GetUserAsync(_botOptions.CurrentValue.OwnerID, cancellationToken).ConfigureAwait(false);
                await _client.ReplyTextAsync(message,
                    string.Format(@"I will post size of images posted in this group.
`{0}listen` - shows current listen mode. Admins can also also change mode using it's name.
`{0}next <gameID>` - pulls next guesswhat game. <gameID> is optional.
`{0}next continue` - checks AP bot for ID of next game.
`{0}next update <gameID>` - updates AP bot with next game ID. <gameID> is optional.
`{0}check <link>` - checks size of linked image.
`{0}posturl <on/off>` - changes if I should post URL of checked image.

I also can store your notes. For more help, use
`{0}notes help`

For help regarding queues system, use
`{0}queues help`

In case of any questions or suggestions, please contact {1} (ID: {2}).
Using Wolfringo library, v0.2.4-rc7",
_botOptions.CurrentValue.CommandPrefix, owner.Nickname, owner.ID.ToString()),
                    cancellationToken).ConfigureAwait(false);
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
