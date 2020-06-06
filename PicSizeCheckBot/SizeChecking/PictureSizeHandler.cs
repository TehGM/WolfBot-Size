using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;

namespace TehGM.WolfBots.PicSizeCheckBot.SizeChecking
{
    public class PictureSizeHandler : IHostedService
    {
        private readonly IHostedWolfClient _client;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<PictureSizeOptions> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _log;

        public PictureSizeHandler(IHostedWolfClient client, ILogger<PictureSizeHandler> logger, IHostEnvironment environment, IHttpClientFactory httpClientFactory, IOptionsMonitor<PictureSizeOptions> options)
        {
            this._client = client;
            this._environment = environment;
            this._options = options;
            this._httpClientFactory = httpClientFactory;
            this._log = logger;

            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            // if not in production, work only in PM for testing
            if (!_environment.IsProduction() && !message.IsPrivateMessage)
                return;

            // work only with image links
            if (message.MimeType != ChatMessageTypes.ImageLink)
                return;

            using IDisposable logScope = _log.BeginScope(new Dictionary<string, object>()
            {
                { "ImageURL", message.Text },
                { "SenderID", message.SenderID.Value },
                { "GroupName", message.IsGroupMessage ? message.RecipientID.ToString() : null }
            });

            Image img = null;
            try
            {
                img = await DownloadImageAsync(message.Text);
            }
            catch (Exception ex) when (ex.LogAsError(this._log, "Failed downloading image {ImageURL}"))
            {
                await _client.RespondWithTextAsync(message, $"/alert Failed downloading image: {ex.Message}\r\nImage URL: {message.Text}").ConfigureAwait(false);
                return;
            }

            _log.LogTrace("Verifying image size");
            PictureSize size = Verify(img.Size);
            _log.LogTrace("Image size: {ImageSize}", size);
            await _client.RespondWithTextAsync(message, $"Image size: {size} {GetEmoteForExpression(!size.IsTooSmall && !size.IsTooBig)}\r\n" +
                $"Is square: {GetEmoteForExpression(size.IsSquare)}\r\n" +
                $"Image URL: {message.Text}");
        }

        private static string GetEmoteForExpression(bool expression)
            => expression ? "(y)" : "(n)";

        public PictureSize Verify(Size size)
            => new PictureSize(size.Width, size.Height, _options.CurrentValue.MinimumValidSize, _options.CurrentValue.MaximumValidSize);

        private async Task<Image> DownloadImageAsync(string imageUrl)
        {
            _log.LogDebug("Downloading image from {ImageURL}", imageUrl);
            HttpClient client = _httpClientFactory.CreateClient();
            using Stream imageStream = await client.GetStreamAsync(imageUrl);
            using MemoryStream memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            return Image.FromStream(memoryStream);
        }


        // Implementing IHostedService ensures this class is created on start
        Task IHostedService.StartAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
        Task IHostedService.StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
