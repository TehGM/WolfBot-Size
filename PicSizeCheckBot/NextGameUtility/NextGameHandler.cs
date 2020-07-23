using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace TehGM.WolfBots.PicSizeCheckBot.NextGameUtility
{
    public class NextGameHandler : IHostedService
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IOptionsMonitor<NextGameOptions> _nextGameOptions;
        private readonly IGroupConfigStore _groupConfigStore;
        private readonly ILogger _log;
        private readonly IHostEnvironment _environment;

        private CancellationTokenSource _cts;
        private readonly Regex _nextCommandRegex = new Regex(@"^next(?:\s(\S+))?", BotCommandUtilities.DefaultRegexOptions);
        private readonly Regex _nextContinueCommandRegex = new Regex(@"^next\scontinue(?:\s(\S+))?", BotCommandUtilities.DefaultRegexOptions);
        private readonly Regex _nextUpdateCommandRegex = new Regex(@"^next\supdate(?:\s(\S+))?", BotCommandUtilities.DefaultRegexOptions);

        public NextGameHandler(IHostedWolfClient client, IGroupConfigStore groupConfigStore, IHostEnvironment environment,
            IOptionsMonitor<BotOptions> botOptions, IOptionsMonitor<NextGameOptions> nextGameOptions, ILogger<NextGameHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._client = client;
            this._groupConfigStore = groupConfigStore;
            this._nextGameOptions = nextGameOptions;
            this._environment = environment;

            // add client listeners
            this._client.AddMessageListener<ChatMessage>(OnChatMessage);
        }

        private async void OnChatMessage(ChatMessage message)
        {
            using IDisposable logScope = message.BeginLogScope(_log);

            // run only in prod, test group or owner PM
            if (!_environment.IsProduction() &&
                !((message.IsGroupMessage && message.RecipientID == _botOptions.CurrentValue.TestGroupID) ||
                (message.IsPrivateMessage && message.RecipientID == _botOptions.CurrentValue.OwnerID)))
                return;

            try
            {
                // check if this is a correct command
                if (!message.IsText || !message.IsGroupMessage)
                    return;
                if (!message.TryGetCommandValue(_botOptions.CurrentValue, out string command))
                    return;

                CancellationToken cancellationToken = _cts?.Token ?? default;

                if (command.StartsWith("next", StringComparison.OrdinalIgnoreCase) && !message.IsGroupMessage)
                {
                    await _client.ReplyTextAsync(message, "(n) Pulling next guesswhat game is possible only in groups.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (_nextUpdateCommandRegex.TryGetMatch(command, out Match updateMatch))
                    await CmdUpdateAsync(message, updateMatch.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
                else if (_nextContinueCommandRegex.TryGetMatch(command, out Match continueMatch))
                    await CmdContinueAsync(message, continueMatch.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
                // this command should always come last!!
                else if (_nextCommandRegex.TryGetMatch(command, out Match nextMatch))
                    await CmdNextAsync(message, nextMatch.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (MessageSendingException ex) when (ex.SentMessage is ChatMessage && ex.Response is WolfResponse response && response.ErrorCode == WolfErrorCode.LoginIncorrectOrCannotSendToGroup) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        private async Task CmdUpdateAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            // check everyone has right permission
            if (!await CheckUserHasAdminAsync(message.SenderID.Value, message.RecipientID, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) You need at least admin privileges to do this.", cancellationToken).ConfigureAwait(false);
                return;
            }
            if (!await CheckUserHasAdminAsync(_client.CurrentUserID.Value, message.RecipientID, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) I need at least admin privileges to do this.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // determine ID to set, if null means failed (message will be sent automatically
            uint? nextID = await GetNextIdAsync(message, userInput, cancellationToken).ConfigureAwait(false);
            if (nextID == null)
                return;

            // start interactive session with AP to update
            NextGameOptions options = this._nextGameOptions.CurrentValue;
            await _client.ReplyTextAsync(message, options.AutoPostBotRemoveCommand, cancellationToken).ConfigureAwait(false);
            ChatMessage botResponse = await _client.AwaitNextGroupByUserAsync(options.AutoPostBotID, message.RecipientID, TimeSpan.FromSeconds(options.AutoPostBotWaitSeconds), cancellationToken).ConfigureAwait(false);
            if (botResponse == null)
                await _client.ReplyTextAsync(message, $"(n) AP didn't respond within {options.AutoPostBotWaitSeconds} seconds.");
            else
                await _client.ReplyTextAsync(message, BotInteractionUtilities.GetAutopostBotAddCommand(options, nextID), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await SaveGroupConfigAsync(message.RecipientID, nextID.Value, cancellationToken).ConfigureAwait(false);
        }

        private async Task CmdContinueAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            // try user input first
            string rawNextID;
            if (!string.IsNullOrWhiteSpace(userInput))
                rawNextID = userInput;
            // if user did not provide ID, start interactive session with AP
            else
            {
                NextGameOptions options = this._nextGameOptions.CurrentValue;
                await _client.ReplyTextAsync(message, options.AutoPostBotPostCommand, cancellationToken).ConfigureAwait(false);
                ChatMessage botResponse = await _client.AwaitNextGroupByUserAsync(options.AutoPostBotID, message.RecipientID, TimeSpan.FromSeconds(options.AutoPostBotWaitSeconds), cancellationToken).ConfigureAwait(false);
                if (botResponse == null)
                {
                    await _client.ReplyTextAsync(message, $"(n) AP didn't respond within {options.AutoPostBotWaitSeconds} seconds.");
                    return;
                }
                rawNextID = botResponse.Text;
            }

            // validate ID
            if (!uint.TryParse(rawNextID, out uint nextID))
            {
                await _client.ReplyTextAsync(message, $"(n) {rawNextID} is not a correct game ID!", cancellationToken).ConfigureAwait(false);
                return;
            }

            // request next
            await _client.ReplyTextAsync(message, BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions.CurrentValue, nextID), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await SaveGroupConfigAsync(message.RecipientID, nextID, cancellationToken).ConfigureAwait(false);
        }

        private async Task CmdNextAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            uint? nextID = await GetNextIdAsync(message, userInput, cancellationToken).ConfigureAwait(false);
            if (nextID == null)
                return;

            // request next
            await _client.ReplyTextAsync(message, BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions.CurrentValue, nextID.Value), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await SaveGroupConfigAsync(message.RecipientID, nextID.Value, cancellationToken).ConfigureAwait(false);
        }

        #region Helpers
        private async Task<bool> SaveGroupConfigAsync(uint groupID, uint currentID, CancellationToken cancellationToken = default)
        {
            try
            {
                GroupConfig groupConfig = await _groupConfigStore.GetGroupConfigAsync(groupID, cancellationToken).ConfigureAwait(false);
                groupConfig.CurrentGuesswhatGameID = currentID;
                await _groupConfigStore.SetGroupConfigAsync(groupConfig, false, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving group config for group {GroupID} in the database", groupID))
            {
                await _client.SendGroupTextMessageAsync(groupID, "/alert Failed saving group config in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private async Task<uint?> GetNextIdAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                GroupConfig groupConfig = await _groupConfigStore.GetGroupConfigAsync(message.RecipientID, cancellationToken).ConfigureAwait(false);
                if (groupConfig == null || groupConfig.CurrentGuesswhatGameID == null)
                {
                    string prefix = _botOptions.CurrentValue.CommandPrefix;
                    await _client.ReplyTextAsync(message, "(n) I do not know the next GW game ID for this group.\r\n" +
                        $"Use `{prefix}next <ID>` to set it, or `{prefix}next continue` to take it from AP", cancellationToken).ConfigureAwait(false);
                    return null;
                }
                else
                    return GetNextExistingID(groupConfig.CurrentGuesswhatGameID.Value);
            }
            if (!uint.TryParse(userInput, out uint nextID))
            {
                await _client.ReplyTextAsync(message, $"(n) `{userInput}` is not a correct game ID!", cancellationToken).ConfigureAwait(false);
                return null;
            }
            else return nextID;
        }

        private uint GetNextExistingID(uint currentID)
        {
            uint[] knownIDs = _nextGameOptions.CurrentValue.KnownGuesswhatIDs;

            // if no known IDs, return +1
            if (knownIDs == null || knownIDs.Length == 0)
                return ++currentID;

            uint start = knownIDs[0];
            uint end = knownIDs[^1];

            // if current ID is smaller than first, return first
            if (currentID < start) return start;
            // if current ID is first, return second
            if (currentID == start && knownIDs.Length > 1) return knownIDs[1];
            // if current ID is same or greater than largest known ID, return +1
            if (currentID >= end) return ++currentID;

            // perform binary search to get current ID position, or position of one smaller if it doesn't exist
            // don't use recursion, just in case
            int startIndex = 0;
            int endIndex = knownIDs.Length - 1;
            while (endIndex - startIndex > 0)
            {
                int middleIndex = startIndex + ((endIndex - startIndex) / 2);
                uint middleID = knownIDs[middleIndex];
                uint nextID = knownIDs[middleIndex + 1];

                // if currentID is middle, or between middle and next, we can just return next
                if (currentID == middleID || (currentID > middleID && currentID < nextID))
                    return nextID;
                // since we already know it, if current ID is next, we can return one after that
                if (nextID == currentID)
                    return knownIDs[middleIndex + 2];

                //if not found yet, keep slashing the collection in half
                if (currentID < middleID)
                    endIndex = middleIndex;
                else
                    startIndex = middleIndex;
            }
            throw new KeyNotFoundException();
        }

        private async Task<bool> CheckUserHasAdminAsync(uint userID, uint groupID, CancellationToken cancellationToken = default)
        {
            WolfGroup group = await _client.GetGroupAsync(groupID, cancellationToken).ConfigureAwait(false);
            WolfGroupMember member = group.Members[userID];
            return member.HasAdminPrivileges;
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
