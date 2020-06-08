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
    public class PictureSizeHandler : IHostedService, IDisposable
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
        private static Regex _listenCommandRegex = new Regex(@"^listen(?:\s(\S*))?(?:\s(\S*)\b)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static Regex _postUrlCommandRegex = new Regex(@"^posturl(?:\s(\S*)\b)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static Regex _enableDisableCommandRegex = new Regex(@"^(enable|disable)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private CancellationTokenSource _cts;

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
            // permit working in private test group
            if (!_environment.IsProduction() && !message.IsPrivateMessage && message.RecipientID != 2790082)
                return;

            CancellationToken cancellationToken = _cts?.Token ?? default;

            try
            {
                if (message.MimeType == ChatMessageTypes.ImageLink)
                {
                    await HandleImageCheckRequestAsync(message, message.Text, false, cancellationToken).ConfigureAwait(false);
                    return;
                }
                else if (message.MimeType == ChatMessageTypes.Text && message.TryGetCommandValue(_botOptions.CurrentValue, out string command))
                {
                    // handle chat commands here

                    /* Check Image by URL */
                    if (command.StartsWith("check ", StringComparison.OrdinalIgnoreCase))
                    {
                        string url = command.Substring("check ".Length).TrimEnd();
                        if (!_urlMatchingRegex.IsMatch(url))
                            await _client.RespondWithTextAsync(message, $"/alert Invalid URL: {url}", cancellationToken).ConfigureAwait(false);
                        else
                            await HandleImageCheckRequestAsync(message, url, true, cancellationToken).ConfigureAwait(false);
                    }
                    /* Select listen mode */
                    else if (_listenCommandRegex.TryGetMatch(command, out Match listenMatch))
                        await CmdListenAsync(message, listenMatch, cancellationToken).ConfigureAwait(false);
                    /* Enable/disable */
                    else if (_enableDisableCommandRegex.TryGetMatch(command, out Match enableDisableMatch))
                        await CmdEnableDisableAsync(message, enableDisableMatch, cancellationToken).ConfigureAwait(false);
                    /* Switch posting image URL */
                    else if (_postUrlCommandRegex.TryGetMatch(command, out Match postUrlMatch))
                        await CmdPostUrlAsync(message, postUrlMatch, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                using IDisposable logScope = _log.BeginScope(new Dictionary<string, object>()
                {
                    { "MessageText", message.Text },
                    { "SenderID", message.SenderID.Value },
                    { "RecipientID", message.RecipientID },
                    { "GroupName", message.IsGroupMessage ? message.RecipientID.ToString() : null }
                });

                _log.LogError(ex, "Error occured when processing message");
            }
        }

        #region Configuration
        private async Task CmdListenAsync(ChatMessage message, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // only work in groups
            if (message.IsPrivateMessage)
            {
                await _client.RespondWithTextAsync(message, "/alert Listen modes are only supported in groups.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check user is admin or owner
            WolfGroupMember member = await GetGroupMemberAsync(message, cancellationToken).ConfigureAwait(false);
            if (member?.HasAdminPrivileges != true)
            {
                await _client.RespondWithTextAsync(message, "/alert You need at least admin permissions to change group config.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check how the user wants to change the setting
            SettingSwitch settingSwitch = ParseSettingSwitch(regexMatch.Groups.Count > 2 ? regexMatch.Groups[2]?.Value : null);
            if (settingSwitch == SettingSwitch.Invalid)
            {
                await _client.RespondWithTextAsync(message, "/alert Invalid switch.\r\n" +
                    "Allowed values: on, true, enable, enabled, off, false, disable, disabled, toggle", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get config
            GroupConfig config = await GetConfigAsync<GroupConfig>(message, cancellationToken).ConfigureAwait(false);

            // change settings based on mode
            string mode = regexMatch.Groups[1]?.Value?.ToLowerInvariant();
            switch (mode)
            {
                // if no mode or switch privided, simply list current settings
                case null:
                case "":
                    await _client.RespondWithTextAsync(message, "Current listen mode settings for this group:\r\n" +
                        $"Admins: {BoolToOnOff(config.ListenAdmins)}\r\n" +
                        $"Mods: {BoolToOnOff(config.ListenMods)}\r\n" +
                        $"Users: {BoolToOnOff(config.ListenUsers)}\r\n" +
                        $"Bots: {BoolToOnOff(config.ListenBots)}\r\n\r\n" +
                        $"Automatic checking is currently {(config.IsEnabled ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
                    return;
                // for help, send help message
                case "help":
                    await _client.RespondWithTextAsync(message, "listen <mode> [switch]\r\n" +
                        "Mode (mandatory): admins, mods, users, bots\r\n" +
                        "Switch (optional): on, off, toggle", cancellationToken).ConfigureAwait(false);
                    return;
                // process each of listen modes
                case "admins":
                case "admin":
                    config.ListenAdmins = GetSwitchedValue(config.ListenAdmins, settingSwitch);
                    await _client.RespondWithTextAsync(message, 
                        $"Listening to admins set to {BoolToOnOff(config.ListenAdmins)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "mods":
                case "mod":
                    config.ListenMods = GetSwitchedValue(config.ListenMods, settingSwitch);
                    await _client.RespondWithTextAsync(message, 
                        $"Listening to mods set to {BoolToOnOff(config.ListenMods)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "users":
                case "user":
                    config.ListenUsers = GetSwitchedValue(config.ListenUsers, settingSwitch);
                    await _client.RespondWithTextAsync(message, 
                        $"Listening to users without role set to {BoolToOnOff(config.ListenUsers)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "bots":
                case "bot":
                    config.ListenBots = GetSwitchedValue(config.ListenBots, settingSwitch);
                    await _client.RespondWithTextAsync(message, 
                        $"Listening to bots set to {BoolToOnOff(config.ListenBots)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await _client.RespondWithTextAsync(message, $"/alert Unknown listening mode: {mode}", cancellationToken).ConfigureAwait(false);
                    return;
            }

            // save settings
            await SaveConfigAsync(config, cancellationToken).ConfigureAwait(false);
        }

        private async Task CmdEnableDisableAsync(ChatMessage message, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // work only in groups
            if (message.IsPrivateMessage)
            {
                await _client.RespondWithTextAsync(message, "/alert Enabling and disabling is only supported in groups.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check user is admin or owner
            WolfGroupMember member = await GetGroupMemberAsync(message, cancellationToken).ConfigureAwait(false);
            if (member?.HasAdminPrivileges != true)
            {
                await _client.RespondWithTextAsync(message, "/alert You need at least admin permissions to change group config.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get config
            GroupConfig config = await GetConfigAsync<GroupConfig>(message, cancellationToken).ConfigureAwait(false);

            // update setting
            switch (regexMatch.Groups[1].Value.ToLowerInvariant())
            {
                case "enable":
                    config.IsEnabled = true;
                    break;
                case "disable":
                    config.IsEnabled = false;
                    break;
            }
            await SaveConfigAsync(config, cancellationToken).ConfigureAwait(false);
            await _client.RespondWithTextAsync(message, 
                $"/me Automatic size checking in this group has been {(config.IsEnabled ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
        }

        private async Task CmdPostUrlAsync(ChatMessage message, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // if is group, ensure user is admin or owner
            if (message.IsGroupMessage)
            {
                WolfGroupMember member = await GetGroupMemberAsync(message, cancellationToken).ConfigureAwait(false);
                if (member?.HasAdminPrivileges != true)
                {
                    await _client.RespondWithTextAsync(message, "/alert You need at least admin permissions to change group config.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // check how the user wants to change the setting
            SettingSwitch settingSwitch = ParseSettingSwitch(regexMatch.Groups.Count > 1 ? regexMatch.Groups[1]?.Value : null);
            if (settingSwitch == SettingSwitch.Invalid)
            {
                await _client.RespondWithTextAsync(message, "/alert Invalid switch.\r\n" +
                    "Allowed values: on, true, enable, enabled, off, false, disable, disabled, toggle", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get and update config
            ITargetConfig config = await GetConfigAsync<ITargetConfig>(message, cancellationToken).ConfigureAwait(false);
            config.PostImageURL = GetSwitchedValue(config.PostImageURL, settingSwitch);
            await SaveConfigAsync(config, cancellationToken).ConfigureAwait(false);
            await _client.RespondWithTextAsync(message, $"Posting image URLs {(config.PostImageURL ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Checking Image Size
        private async Task HandleImageCheckRequestAsync(ChatMessage message, string imageUrl, bool isExplicitRequest, CancellationToken cancellationToken = default)
        {
            ITargetConfig config = await GetConfigAsync<ITargetConfig>(message, cancellationToken).ConfigureAwait(false);
            if (!isExplicitRequest && !await CheckShouldCheckSizeAsync(message, config, cancellationToken).ConfigureAwait(false))
                return;

            using IDisposable logScope = _log.BeginScope(new Dictionary<string, object>()
            {
                { "ImageURL", imageUrl },
                { "SenderID", message.SenderID.Value },
                { "RecipientID", message.RecipientID },
                { "GroupName", message.IsGroupMessage ? message.RecipientID.ToString() : null }
            });

            Image img = null;
            try
            {
                img = await DownloadImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.LogAsError(this._log, "Failed downloading image {ImageURL}", cancellationToken))
            {
                await _client.RespondWithTextAsync(message, $"/alert Failed downloading image: {ex.Message}\r\nImage URL: {imageUrl}", cancellationToken).ConfigureAwait(false);
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
            await _client.RespondWithTextAsync(message, response, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<bool> CheckShouldCheckSizeAsync(ChatMessage message, ITargetConfig config, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Determining if should check the image size");
            if (config is GroupConfig groupConfig)
            {
                // if disabled for all, can return early
                if (!groupConfig.IsEnabled || (!groupConfig.ListenUsers && !groupConfig.ListenMods && !groupConfig.ListenAdmins && !groupConfig.ListenBots))
                    return false;

                // check for all permissions
                WolfGroupMember member = await GetGroupMemberAsync(message, cancellationToken).ConfigureAwait(false);
                if (groupConfig.ListenUsers && member.Capabilities == WolfGroupCapabilities.User)
                    return true;
                if (groupConfig.ListenMods && member.Capabilities == WolfGroupCapabilities.Mod)
                    return true;
                if (groupConfig.ListenAdmins && member.HasAdminPrivileges)
                    return true;
                if (groupConfig.ListenBots)
                {
                    WolfUser user = await _client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                    return user.Device == WolfDevice.Bot;
                }

                // return false if all privilege checks failed
                return false;
            }
            else return true;
        }

        #endregion

        #region Config Helpers
        private static bool GetSwitchedValue(bool existingValue, SettingSwitch settingSwitch)
        {
            if (settingSwitch == SettingSwitch.Invalid)
                throw new ArgumentException("Cannot change setting for invalid switch", nameof(settingSwitch));
            if (settingSwitch == SettingSwitch.Toggle)
                return !existingValue;
            if (settingSwitch == SettingSwitch.On)
                return true;
            if (settingSwitch == SettingSwitch.Off)
                return false;
            throw new ArgumentException($"Unknown switch type {settingSwitch}", nameof(settingSwitch));
        }

        private static string BoolToOnOff(bool value)
            => value ? "on" : "off";

        private SettingSwitch ParseSettingSwitch(string value)
        {
            if (value == null)
                return SettingSwitch.Toggle;

            switch (value.ToLowerInvariant())
            {
                case "on":
                case "true":
                case "enable":
                case "enabled":
                    return SettingSwitch.On;
                case "off":
                case "false":
                case "disable":
                case "disabled":
                    return SettingSwitch.Off;
                case "toggle":
                case "":
                    return SettingSwitch.Toggle;
                default:
                    return SettingSwitch.Invalid;
            }
        }

        private enum SettingSwitch
        {
            On, Off, Toggle, Invalid
        }

        private async Task<T> GetConfigAsync<T>(ChatMessage message, CancellationToken cancellationToken = default) where T : class, ITargetConfig
        {
            if (message.IsGroupMessage)
                return await _groupConfigStore.GetGroupConfigAsync(message.RecipientID, cancellationToken).ConfigureAwait(false) as T;
            else
                return await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false) as T;
        }

        private Task SaveConfigAsync(ITargetConfig config, CancellationToken cancellationToken = default)
        {
            if (config is GroupConfig groupConfig)
                return _groupConfigStore.SetGroupConfigAsync(groupConfig, cancellationToken);
            else if (config is UserData userData)
                return _userDataStore.SetUserDataAsync(userData, cancellationToken);
            return Task.CompletedTask;
        }
        #endregion

        private async Task<WolfGroupMember> GetGroupMemberAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            WolfGroup group = await _client.GetGroupAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);
            return group?.Members[message.SenderID.Value];
        }


        #region Size check Helpers
        private static string GetEmoteForExpression(bool expression)
            => expression ? "(y)" : "(n)";

        public PictureSize Verify(Size size)
            => new PictureSize(size.Width, size.Height, _picSizeOptions.CurrentValue.MinimumValidSize, _picSizeOptions.CurrentValue.MaximumValidSize);

        private async Task<Image> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            _log.LogDebug("Downloading image from {ImageURL}", imageUrl);
            HttpClient client = _httpClientFactory.CreateClient();
            using Stream imageStream = await client.GetStreamAsync(imageUrl).ConfigureAwait(false);
            using MemoryStream memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return Image.FromStream(memoryStream);
        }
        #endregion

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
