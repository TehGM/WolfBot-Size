using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public class TempMessageHandler : IHostedService
    {
        private readonly IHostedWolfClient _client;

        public TempMessageHandler(IHostedWolfClient client)
        {
            this._client = client;
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            if (message.IsPrivateMessage && message.IsText && message.Text.StartsWith("hello", StringComparison.OrdinalIgnoreCase))
                await _client.RespondWithTextAsync(message, "Hello there!").ConfigureAwait(false);
        }

        // Implementing IHostedService ensures this class is created on start
        Task IHostedService.StartAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
        Task IHostedService.StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
