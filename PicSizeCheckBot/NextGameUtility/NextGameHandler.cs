using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Utilities;

namespace TehGM.WolfBots.PicSizeCheckBot.NextGameUtility
{
    [CommandsHandler]
    public class NextGameHandler
    {
        private readonly BotOptions _botOptions;
        private readonly NextGameOptions _nextGameOptions;
        private readonly IGroupConfigStore _groupConfigStore;
        private readonly ILogger _log;

        public NextGameHandler(IGroupConfigStore groupConfigStore, IOptionsSnapshot<BotOptions> botOptions, IOptionsSnapshot<NextGameOptions> nextGameOptions, ILogger<NextGameHandler> log)
        {
            // store all services
            this._log = log;
            this._botOptions = botOptions.Value;
            this._groupConfigStore = groupConfigStore;
            this._nextGameOptions = nextGameOptions.Value;
        }

        [RegexCommand(@"^next\supdate(?:\s(\S+))?")]
        [Priority(-11)]
        [GroupOnly]
        [RequireBotGroupAdmin]
        [RequireGroupAdmin]
        public async Task CmdUpdateAsync(CommandContext context, string userInput = null, CancellationToken cancellationToken = default)
        {
            // determine ID to set, if null means failed (message will be sent automatically
            uint? nextID = await this.GetNextIdAsync(context, userInput, cancellationToken).ConfigureAwait(false);
            if (nextID == null)
                return;

            // start interactive session with AP to update
            await context.ReplyTextAsync(this._nextGameOptions.AutoPostBotRemoveCommand, cancellationToken).ConfigureAwait(false);
            ChatMessage botResponse = await context.Client.AwaitNextGroupByUserAsync(this._nextGameOptions.AutoPostBotID, context.Message.RecipientID, TimeSpan.FromSeconds(this._nextGameOptions.AutoPostBotWaitSeconds), cancellationToken).ConfigureAwait(false);
            if (botResponse == null)
                await context.ReplyTextAsync($"(n) AP didn't respond within {this._nextGameOptions.AutoPostBotWaitSeconds} seconds.");
            else
                await context.ReplyTextAsync(BotInteractionUtilities.GetAutopostBotAddCommand(this._nextGameOptions, nextID), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await this.SaveGroupConfigAsync(context.Client, context.Message.RecipientID, nextID.Value, cancellationToken).ConfigureAwait(false);
        }

        [RegexCommand(@"^next\scontinue(?:\s(\S+))?")]
        [Priority(-10)]
        [GroupOnly]
        public async Task CmdContinueAsync(CommandContext context, string userInput = null, CancellationToken cancellationToken = default)
        {
            // try user input first
            string rawNextID;
            if (!string.IsNullOrWhiteSpace(userInput))
                rawNextID = userInput;
            // if user did not provide ID, start interactive session with AP
            else
            {
                await context.ReplyTextAsync(this._nextGameOptions.AutoPostBotPostCommand, cancellationToken).ConfigureAwait(false);
                ChatMessage botResponse = await context.Client.AwaitNextGroupByUserAsync(this._nextGameOptions.AutoPostBotID, context.Message.RecipientID, TimeSpan.FromSeconds(this._nextGameOptions.AutoPostBotWaitSeconds), cancellationToken).ConfigureAwait(false);
                if (botResponse == null)
                {
                    await context.ReplyTextAsync($"(n) AP didn't respond within {this._nextGameOptions.AutoPostBotWaitSeconds} seconds.");
                    return;
                }
                rawNextID = botResponse.Text;
            }

            // validate ID
            if (!uint.TryParse(rawNextID, out uint nextID))
            {
                await context.ReplyTextAsync($"(n) {rawNextID} is not a correct game ID!", cancellationToken).ConfigureAwait(false);
                return;
            }

            // request next
            await context.ReplyTextAsync(BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions, nextID), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await this.SaveGroupConfigAsync(context.Client, context.Message.RecipientID, nextID, cancellationToken).ConfigureAwait(false);
        }

        [RegexCommand(@"^next(?:\s(\S+))?")]
        [Priority(-12)]
        [GroupOnly]
        public async Task CmdNextAsync(CommandContext context, string userInput = null, CancellationToken cancellationToken = default)
        {
            uint? nextID = await this.GetNextIdAsync(context, userInput, cancellationToken).ConfigureAwait(false);
            if (nextID == null)
                return;

            // request next
            await context.ReplyTextAsync(BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions, nextID.Value), cancellationToken).ConfigureAwait(false);

            // update config and save in DB
            await this.SaveGroupConfigAsync(context.Client, context.Message.RecipientID, nextID.Value, cancellationToken).ConfigureAwait(false);
        }

        #region Helpers
        private async Task<bool> SaveGroupConfigAsync(IWolfClient client, uint groupID, uint currentID, CancellationToken cancellationToken = default)
        {
            try
            {
                GroupConfig groupConfig = await this._groupConfigStore.GetGroupConfigAsync(groupID, cancellationToken).ConfigureAwait(false);
                groupConfig.CurrentGuesswhatGameID = currentID;
                await _groupConfigStore.SetGroupConfigAsync(groupConfig, false, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving group config for group {GroupID} in the database", groupID))
            {
                await client.SendGroupTextMessageAsync(groupID, "/alert Failed saving group config in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private async Task<uint?> GetNextIdAsync(CommandContext context, string userInput, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                GroupConfig groupConfig = await this._groupConfigStore.GetGroupConfigAsync(context.Message.RecipientID, cancellationToken).ConfigureAwait(false);
                if (groupConfig == null || groupConfig.CurrentGuesswhatGameID == null)
                {
                    await context.ReplyTextAsync("(n) I do not know the next GW game ID for this group.\r\n" +
                        $"Use `{context.Options.Prefix}next <ID>` to set it, or `{context.Options.Prefix}next continue` to take it from AP", cancellationToken).ConfigureAwait(false);
                    return null;
                }
                else
                    return this.GetNextExistingID(groupConfig.CurrentGuesswhatGameID.Value);
            }
            if (!uint.TryParse(userInput, out uint nextID))
            {
                await context.ReplyTextAsync($"(n) `{userInput}` is not a correct game ID!", cancellationToken).ConfigureAwait(false);
                return null;
            }
            else return nextID;
        }

        private uint GetNextExistingID(uint currentID)
        {
            uint[] knownIDs = this._nextGameOptions.KnownGuesswhatIDs;

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
        #endregion
    }
}
