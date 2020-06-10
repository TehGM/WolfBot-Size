using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
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
                    string.Format(@"I will post size of images posted in this group, unless this feature is disabled by bot admin.
You can check when will I measure pics using
`{0}listen`
To pull next guesswhat game, use 
`{0}next`
You can set starting game to pull using 
`{0}next <gameID>`
Alternatively, I can check ap bot what was last game and automatically start from it
`{0}next continue`
I am able to update AP bot first message with ID if I am an admin. Simply use
`{0}next update <gameID>`
<gameID> is optional - if not specified, I'll use the same ID I'd use for ""{0}next""
Also, I can post image from link and automatically post it's size
`{0}check <link>`
You can decide whether I should post URLs for checked images using
`{0}posturl`

I also can store your notes. For more help, use
`{0}notes help`

For help regarding queues system, use
`{0}queues help`

In case of any questions or suggestions, please contact {1} (ID: {2}).",
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
