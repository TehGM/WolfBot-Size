using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
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
        private readonly IOptionsMonitor<PictureSizeOptions> _picSizeOptions;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserDataStore _userDataStore;
        private readonly IGroupConfigStore _groupConfigStore;
        private readonly ILogger _log;

        private Regex _urlMatchingRegex;

        public PictureSizeHandler(IHostedWolfClient client, 
            ILogger<PictureSizeHandler> logger, IHostEnvironment environment, IHttpClientFactory httpClientFactory,
            IUserDataStore userDataStore, IGroupConfigStore groupConfigStore,
            IOptionsMonitor<PictureSizeOptions> picSizeOptions, IOptionsMonitor<BotOptions> botOptions)
        {
            // store all services
            this._client = client;
            this._log = logger;
            this._environment = environment;
            this._httpClientFactory = httpClientFactory;
            this._userDataStore = userDataStore;
            this._groupConfigStore = groupConfigStore;
            this._picSizeOptions = picSizeOptions;
            this._botOptions = botOptions;

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);

            // read options
            this.OnPicSizeOptionsReload(picSizeOptions.CurrentValue);
            picSizeOptions.OnChange(this.OnPicSizeOptionsReload);
        }

        private void OnPicSizeOptionsReload(PictureSizeOptions options)
        {
            this._urlMatchingRegex = new Regex(options.UrlMatchingPattern);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            // if not in production, work only in PM for testing
            if (!_environment.IsProduction() && !message.IsPrivateMessage)
                return;

            if (message.MimeType == ChatMessageTypes.ImageLink)
            {
                await HandleImageCheckRequestAsync(message, message.Text).ConfigureAwait(false);
                return;
            }
            else if (message.MimeType == ChatMessageTypes.Text && message.TryGetCommandValue(_botOptions.CurrentValue, out string command) && command.StartsWith("check ", StringComparison.OrdinalIgnoreCase))
            {
                string url = command.Substring("check ".Length).TrimEnd();
                if (!_urlMatchingRegex.IsMatch(url))
                    await _client.RespondWithTextAsync(message, $"/alert Invalid URL: {url}").ConfigureAwait(false);
                else
                    await HandleImageCheckRequestAsync(message, url).ConfigureAwait(false);
                return;
            }
        }

        private async Task HandleImageCheckRequestAsync(ChatMessage message, string imageUrl)
        {
            ITargetConfig config = await GetConfigAsync(message).ConfigureAwait(false);
            if (!await CheckShouldSendAsync(message, config).ConfigureAwait(false))
                return;

            using IDisposable logScope = _log.BeginScope(new Dictionary<string, object>()
            {
                { "ImageURL", imageUrl },
                { "SenderID", message.SenderID.Value },
                { "GroupName", message.IsGroupMessage ? message.RecipientID.ToString() : null }
            });

            Image img = null;
            try
            {
                img = await DownloadImageAsync(imageUrl).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.LogAsError(this._log, "Failed downloading image {ImageURL}"))
            {
                await _client.RespondWithTextAsync(message, $"/alert Failed downloading image: {ex.Message}\r\nImage URL: {imageUrl}").ConfigureAwait(false);
                return;
            }

            _log.LogTrace("Verifying image size");
            PictureSize size = Verify(img.Size);
            _log.LogTrace("Image size: {ImageSize}", size);

            // build message
            string response = $"Image size: {size} {GetEmoteForExpression(!size.IsTooSmall && !size.IsTooBig)}\r\n" +
                $"Is square: {GetEmoteForExpression(size.IsSquare)}";
            if (config.PostImageURL)
                response += $"\r\nImage URL: {imageUrl}";

            // send the response
            await _client.RespondWithTextAsync(message, response).ConfigureAwait(false);
        }

        private async Task<ITargetConfig> GetConfigAsync(ChatMessage message)
        {
            if (message.IsGroupMessage)
                return await _groupConfigStore.GetGroupConfigAsync(message.RecipientID).ConfigureAwait(false);
            else
                return await _userDataStore.GetUserDataAsync(message.SenderID.Value).ConfigureAwait(false);
        }

        private async ValueTask<bool> CheckShouldSendAsync(ChatMessage message, ITargetConfig config)
        {
            _log.LogTrace("Determining if should check the image size");
            if (config is GroupConfig groupConfig)
            {
                // if disabled for all, can return early
                if (!groupConfig.IsEnabled || (!groupConfig.ListenUsers && !groupConfig.ListenMods && !groupConfig.ListenAdmins && !groupConfig.ListenBots))
                    return false;
                WolfGroup group = await _client.GetGroupAsync(message.RecipientID).ConfigureAwait(false);
                WolfGroupMember member = group.Members[message.SenderID.Value];

                // check for all permissions
                if (groupConfig.ListenUsers && member.Capabilities == WolfGroupCapabilities.User)
                    return true;
                if (groupConfig.ListenMods && member.Capabilities == WolfGroupCapabilities.Mod)
                    return true;
                if (groupConfig.ListenAdmins && member.HasAdminPrivileges)
                    return true;
                if (groupConfig.ListenBots)
                {
                    WolfUser user = await _client.GetUserAsync(message.SenderID.Value);
                    return user.Device == WolfDevice.Bot;
                }

                // return false if all privilege checks failed
                return false;
            }
            else return true;
        }

        private static string GetEmoteForExpression(bool expression)
            => expression ? "(y)" : "(n)";

        public PictureSize Verify(Size size)
            => new PictureSize(size.Width, size.Height, _picSizeOptions.CurrentValue.MinimumValidSize, _picSizeOptions.CurrentValue.MaximumValidSize);

        private async Task<Image> DownloadImageAsync(string imageUrl)
        {
            _log.LogDebug("Downloading image from {ImageURL}", imageUrl);
            HttpClient client = _httpClientFactory.CreateClient();
            using Stream imageStream = await client.GetStreamAsync(imageUrl).ConfigureAwait(false);
            using MemoryStream memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream).ConfigureAwait(false);
            return Image.FromStream(memoryStream);
        }


        // Implementing IHostedService ensures this class is created on start
        Task IHostedService.StartAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
        Task IHostedService.StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
