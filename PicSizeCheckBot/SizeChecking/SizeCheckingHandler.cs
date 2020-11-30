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
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;
using TehGM.Wolfringo.Utilities;

namespace TehGM.WolfBots.PicSizeCheckBot.SizeChecking
{
    [CommandsHandler(IsPersistent = true)]
    public class SizeCheckingHandler : IDisposable
    {
        private readonly IWolfClient _client;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<SizeCheckingOptions> _picSizeOptions;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IOptionsMonitor<CommandsOptions> _commandsOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserDataStore _userDataStore;
        private readonly IGroupConfigStore _groupConfigStore;
        private readonly ILogger _log;

        private Regex _urlMatchingRegex;
        private readonly CancellationTokenSource _cts;

        public SizeCheckingHandler(IWolfClient client, 
            ILogger<SizeCheckingHandler> logger, IHostEnvironment environment, IHttpClientFactory httpClientFactory,
            IUserDataStore userDataStore, IGroupConfigStore groupConfigStore,
            IOptionsMonitor<SizeCheckingOptions> picSizeOptions, IOptionsMonitor<BotOptions> botOptions, IOptionsMonitor<CommandsOptions> commandsOptions)
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
            this._commandsOptions = commandsOptions;

            this._cts = new CancellationTokenSource();

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);

            // read options
            this.OnPicSizeOptionsReload(picSizeOptions.CurrentValue);
            picSizeOptions.OnChange(this.OnPicSizeOptionsReload);
        }

        private void OnPicSizeOptionsReload(SizeCheckingOptions options)
        {
            this._urlMatchingRegex = new Regex(options.UrlMatchingPattern);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            // run only in prod, test group or owner PM
            if (!_environment.IsProduction() &&
                !((message.IsGroupMessage && message.RecipientID == _botOptions.CurrentValue.TestGroupID) ||
                (message.IsPrivateMessage && message.SenderID == _botOptions.CurrentValue.OwnerID)))
                return;

            CommandContext context = new CommandContext(message, this._client, this._commandsOptions.CurrentValue);
            using IDisposable logScope = _log.BeginCommandScope(context, this);
            try
            {
                if (message.MimeType == ChatMessageTypes.ImageLink)
                {
                    await HandleImageCheckRequestAsync(context, message.Text, false, this._cts.Token).ConfigureAwait(false);
                    return;
                }
            }
            catch (TaskCanceledException) { }
            catch (MessageSendingException ex) when (ex.SentMessage is ChatMessage && ex.Response is WolfResponse response && response.ErrorCode == WolfErrorCode.LoginIncorrectOrCannotSendToGroup) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        #region Configuration
        [RegexCommand(@"^listen(?:\s(\S*))?(?:\s(\S*)\b)?")]
        [GroupOnly]
        private async Task CmdListenAsync(CommandContext context, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // check user is admin or owner
            string mode = regexMatch.Groups[1]?.Value?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(mode))
            {
                WolfGroupMember member = await GetGroupMemberAsync(context, cancellationToken).ConfigureAwait(false);
                if (member?.HasAdminPrivileges != true)
                {
                    await context.ReplyTextAsync("/alert You need at least admin permissions to change group config.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // check how the user wants to change the setting
            SettingSwitch settingSwitch = ParseSettingSwitch(regexMatch.Groups.Count > 2 ? regexMatch.Groups[2]?.Value : null);
            if (settingSwitch == SettingSwitch.Invalid)
            {
                await context.ReplyTextAsync("/alert Invalid switch.\r\n" +
                    "Allowed values: on, true, enable, enabled, off, false, disable, disabled, toggle", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get config
            GroupConfig config = await GetConfigAsync<GroupConfig>(context.Message, cancellationToken).ConfigureAwait(false);

            // change settings based on mode
            switch (mode)
            {
                // if no mode or switch privided, simply list current settings
                case null:
                case "":
                    await context.ReplyTextAsync("Current listen mode settings for this group:\r\n" +
                        $"Admins: {BoolToOnOff(config.ListenAdmins)}\r\n" +
                        $"Mods: {BoolToOnOff(config.ListenMods)}\r\n" +
                        $"Users: {BoolToOnOff(config.ListenUsers)}\r\n" +
                        $"Bots: {BoolToOnOff(config.ListenBots)}\r\n\r\n" +
                        $"Automatic checking is currently {(config.IsEnabled ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
                    return;
                // for help, send help message
                case "help":
                    await context.ReplyTextAsync("listen <mode> [switch]\r\n" +
                        "Mode (mandatory): admins, mods, users, bots\r\n" +
                        "Switch (optional): on, off, toggle", cancellationToken).ConfigureAwait(false);
                    return;
                // process each of listen modes
                case "admins":
                case "admin":
                    config.ListenAdmins = GetSwitchedValue(config.ListenAdmins, settingSwitch);
                    await context.ReplyTextAsync($"/me Listening to admins set to {BoolToOnOff(config.ListenAdmins)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "mods":
                case "mod":
                    config.ListenMods = GetSwitchedValue(config.ListenMods, settingSwitch);
                    await context.ReplyTextAsync($"/me Listening to mods set to {BoolToOnOff(config.ListenMods)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "users":
                case "user":
                    config.ListenUsers = GetSwitchedValue(config.ListenUsers, settingSwitch);
                    await context.ReplyTextAsync($"/me Listening to users without role set to {BoolToOnOff(config.ListenUsers)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                case "bots":
                case "bot":
                    config.ListenBots = GetSwitchedValue(config.ListenBots, settingSwitch);
                    await context.ReplyTextAsync($"/me Listening to bots set to {BoolToOnOff(config.ListenBots)} (y)", cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await context.ReplyTextAsync($"/alert Unknown listening mode: {mode}", cancellationToken).ConfigureAwait(false);
                    return;
            }

            // save settings
            await SaveConfigAsync(config, cancellationToken).ConfigureAwait(false);
        }

        [RegexCommand(@"^(enable|disable)\b")]
        [GroupOnly]
        [RequireGroupAdmin]
        private async Task CmdEnableDisableAsync(CommandContext context, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // get config
            GroupConfig config = await GetConfigAsync<GroupConfig>(context.Message, cancellationToken).ConfigureAwait(false);

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
            await context.ReplyTextAsync($"/me Automatic size checking in this group has been {(config.IsEnabled ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
        }

        [RegexCommand(@"^posturl(?:\s(\S*)\b)?")]
        private async Task CmdPostUrlAsync(CommandContext context, Match regexMatch, CancellationToken cancellationToken = default)
        {
            // if is group, ensure user is admin or owner
            if (context.IsGroup)
            {
                WolfGroupMember member = await GetGroupMemberAsync(context, cancellationToken).ConfigureAwait(false);
                if (member == null)
                {
                    await context.Client.SendGroupMembersBugNoticeAsync(context.Message, cancellationToken).ConfigureAwait(false);
                    return;
                }
                if (member?.HasAdminPrivileges != true)
                {
                    await context.ReplyTextAsync("/alert You need at least admin permissions to change group config.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // check how the user wants to change the setting
            SettingSwitch settingSwitch = ParseSettingSwitch(regexMatch.Groups.Count > 1 ? regexMatch.Groups[1]?.Value : null);
            if (settingSwitch == SettingSwitch.Invalid)
            {
                await context.ReplyTextAsync("/alert Invalid switch.\r\n" +
                    "Allowed values: on, true, enable, enabled, off, false, disable, disabled, toggle", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get and update config
            ITargetConfig config = await GetConfigAsync<ITargetConfig>(context.Message, cancellationToken).ConfigureAwait(false);
            config.PostImageURL = GetSwitchedValue(config.PostImageURL, settingSwitch);
            await SaveConfigAsync(config, cancellationToken).ConfigureAwait(false);
            await context.ReplyTextAsync($"/me Posting image URLs {(config.PostImageURL ? "enabled" : "disabled")}.", cancellationToken).ConfigureAwait(false);
        }

        [Command("check")]
        private async Task CmdCheckAsync(CommandContext context, [MissingError("Please provide picture URL.")] string imageUrl, CancellationToken cancellationToken = default)
        {
            if (!_urlMatchingRegex.IsMatch(imageUrl))
                await context.ReplyTextAsync($"/alert Invalid URL: {imageUrl}", cancellationToken).ConfigureAwait(false);
            else
                await HandleImageCheckRequestAsync(context, imageUrl, true, cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Checking Image Size
        private async Task HandleImageCheckRequestAsync(CommandContext context, string imageUrl, bool isExplicitRequest, CancellationToken cancellationToken = default)
        {
            ITargetConfig config = await GetConfigAsync<ITargetConfig>(context.Message, cancellationToken).ConfigureAwait(false);
            if (!isExplicitRequest && !await CheckShouldCheckSizeAsync(context, config, cancellationToken).ConfigureAwait(false))
                return;

            using IDisposable logScope = _log.BeginScope(new Dictionary<string, object>()
            {
                { "ImageURL", imageUrl },
                { "SenderID", context.Message.SenderID.Value },
                { "RecipientID", context.Message.RecipientID },
                { "GroupName", context.IsGroup ? context.Message.RecipientID.ToString() : null }
            });

            Image img = null;
            try
            {
                img = await DownloadImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.LogAsError(this._log, "Failed downloading image {ImageURL}", imageUrl))
            {
                await context.ReplyTextAsync($"/alert Failed downloading image: {ex.Message}\r\nImage URL: {imageUrl}", cancellationToken).ConfigureAwait(false);
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
            await context.ReplyTextAsync(response, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<bool> CheckShouldCheckSizeAsync(CommandContext context, ITargetConfig config, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Determining if should check the image size");
            if (config is GroupConfig groupConfig)
            {
                // if disabled for all, can return early
                if (!groupConfig.IsEnabled || (!groupConfig.ListenUsers && !groupConfig.ListenMods && !groupConfig.ListenAdmins && !groupConfig.ListenBots))
                    return false;

                // check for all permissions
                WolfGroupMember member = await GetGroupMemberAsync(context, cancellationToken).ConfigureAwait(false);
                if (groupConfig.ListenUsers && member.Capabilities == WolfGroupCapabilities.User)
                    return true;
                if (groupConfig.ListenMods && member.Capabilities == WolfGroupCapabilities.Mod)
                    return true;
                if (groupConfig.ListenAdmins && member.HasAdminPrivileges)
                    return true;
                if (groupConfig.ListenBots)
                {
                    WolfUser user = await context.GetSenderAsync(cancellationToken).ConfigureAwait(false);
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

        private async Task<WolfGroupMember> GetGroupMemberAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            WolfGroup group = await context.GetRecipientAsync<WolfGroup>(cancellationToken).ConfigureAwait(false);
            if (group == null)
                return null;
            if (group.Members.TryGetValue(context.Message.SenderID.Value, out WolfGroupMember result))
                return result;
            else return null;
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


        public void Dispose()
        {
            this._client?.RemoveMessageListener<ChatMessage>(OnChatMessage);
            try { this._cts?.Cancel(); } catch { }
            try { this._cts?.Dispose(); } catch { }
        }
    }
}
