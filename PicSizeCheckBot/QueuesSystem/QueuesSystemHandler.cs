using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Utilities;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    [CommandsHandler]
    public class QueuesSystemHandler
    {
        private readonly QueuesSystemOptions _queuesOptions;
        private readonly BotOptions _botOptions;
        private readonly IIdQueueStore _idQueueStore;
        private readonly IUserDataStore _userDataStore;
        private readonly ILogger _log;

        public QueuesSystemHandler(IIdQueueStore idQueueStore, IUserDataStore userDataStore, IOptionsSnapshot<QueuesSystemOptions> queuesOptions, IOptionsSnapshot<BotOptions> botOptions, ILogger<QueuesSystemHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions.Value;
            this._queuesOptions = queuesOptions.Value;
            this._idQueueStore = idQueueStore;
            this._userDataStore = userDataStore;
        }

        #region Commands
        [Command("queue help")]
        [Command("queues help")]
        private async Task CmdHelpAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            WolfUser owner = await context.Client.GetUserAsync(_botOptions.OwnerID, cancellationToken).ConfigureAwait(false);
            await context.ReplyTextAsync(string.Format(@"Queue commands:
`{0}<queue name> queue next` - pulls the next ID from <queue name>
`{0}<queue name> queue add <IDs>` - adds IDs
`{0}<queue name> queue show` - shows all IDs
`{0}<queue name> queue remove <IDs>` - removes selected IDs
`{0}<queue name> queue clear` - removes all IDs
`{0}<queue name> queue rename <new name>` - changes name
`{0}<queue name> queue claim` - claims the queue, so you can use ""my"" as it's name
`{0}<queue name> queue transfer <user ID>` - transfers ownership of the queue
`{0}<queue name> info` - shows info about the queue

`clear` and `remove` can only be used on your own queue, or a queue without an owner.
`rename` and `transfer` can only be used if you own the queue.
`claim` can only be used if the queue isn't already claimed. You can check that using `info`.

For bug reports or suggestions, contact {1} (ID: {2})",
context.Options.Prefix, owner.Nickname, owner.ID),
cancellationToken).ConfigureAwait(false);
        }

        /* NEXT */
        [RegexCommand(@"^(.+)?\squeue\snext")]
        [RegexCommand(@"^(.+)?\squeue\s*$")]
        [Priority(-30)]
        [GroupOnly]
        private async Task CmdNextAsync(CommandContext context, string queueName, CancellationToken cancellationToken = default)
        {
            // get queue and ensure not empty
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name
            if (!queue.QueuedIDs.Any())
            {
                await context.ReplyTextAsync($"Queue {queue.Name} is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            uint gameID = queue.QueuedIDs.Dequeue();
            await context.ReplyTextAsync(BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions, gameID), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
        }

        /* ADD */
        [RegexCommand(@"^(.+)?\squeue(?:\sadd)?(?:\s(.*))?$")]
        [Priority(-32)]
        private async Task CmdAddAsync(CommandContext context, string queueName,
            [MissingError("(n) Please provide IDs to add.")] string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // perform adding
            (HashSet<uint> ids, HashSet<string> needsRevisit) = GetIDsFromArgs(args);
            int addedCount = 0;
            int skippedCount = 0;
            foreach (uint i in ids)
            {
                if (queue.QueuedIDs.Contains(i))
                    skippedCount++;
                else
                {
                    queue.QueuedIDs.Enqueue(i);
                    addedCount++;
                }
            }

            // build response
            StringBuilder builder = new StringBuilder();
            if (addedCount > 0)
                builder.AppendFormat("(y) Added {0} IDs to {1} queue.", addedCount.ToString(), queue.Name);
            else
                builder.AppendFormat("(n) No ID added to {0} queue.", queue.Name);
            if (skippedCount > 0)
                builder.AppendFormat("\r\n{0} IDs already exist on the queue so were skipped.", skippedCount.ToString());
            if (needsRevisit.Count > 0)
                builder.AppendFormat("\r\n(n) {0} values weren't valid IDs, but contain digits, so may need revisiting: {1}", needsRevisit.Count.ToString(), string.Join(", ", needsRevisit));

            // send response
            await context.ReplyTextAsync(builder.ToString(), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
        }

        /* SHOW */
        [RegexCommand(@"^(.+)?\squeue\sshow")]
        [Priority(-31)]
        private async Task CmdShowAsync(CommandContext context, string queueName, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            if (queue.QueuedIDs?.Any() != true)
            {
                await context.ReplyTextAsync($"`{queue.Name}` queue is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            bool plural = queue.QueuedIDs.Count > 1;
            await context.ReplyTextAsync($"Currently there {(plural ? "are" : "is")} {queue.QueuedIDs.Count} ID{(plural ? "s" : "")} on {queue.Name} queue:\r\n{string.Join(", ", queue.QueuedIDs)}");
        }

        /* REMOVE */
        [RegexCommand(@"^(.+)?\squeue\sremove(?:\s(.*))?$")]
        [Priority(-31)]
        private async Task CmdRemoveAsync(CommandContext context, string queueName,
            [MissingError("(n) Please provide IDs to remove.")] string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await context.ReplyTextAsync("(n) To remove from a queue, you need to be its owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // perform removing
            (HashSet<uint> ids, HashSet<string> needsRevisit) = GetIDsFromArgs(args);
            int previousCount = queue.QueuedIDs.Count;
            queue.QueuedIDs = new Queue<uint>(queue.QueuedIDs.Where(i => !ids.Contains(i)));


            // build response
            int removedCount = previousCount - queue.QueuedIDs.Count;
            int skippedCount = ids.Count - previousCount;
            StringBuilder builder = new StringBuilder();
            if (removedCount > 0)
                builder.AppendFormat("(y) Removed {0} IDs from {1} queue.", removedCount.ToString(), queue.Name);
            else
                builder.AppendFormat("(n) No ID added to {0} queue.", queue.Name);
            if (skippedCount > 0)
                builder.AppendFormat("\r\n{0} IDs did not exist on the queue so were skipped.", skippedCount.ToString());

            // send response
            await context.ReplyTextAsync(builder.ToString(), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
        }

        /* CLEAR */
        [RegexCommand(@"^(.+)?\squeue\sclear")]
        [Priority(-31)]
        private async Task CmdClearAsync(CommandContext context, string queueName, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await context.ReplyTextAsync("(n) To clear a queue, you need to be its owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
            }

            int idCount = queue.QueuedIDs.Count;
            queue.QueuedIDs.Clear();
            await context.ReplyTextAsync($"(y) {idCount} ID{(idCount > 1 ? "s" : "")} removed from queue \"{queue.Name}\" .", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
        }

        /* RENAME */
        [RegexCommand(@"^(.+)?\squeue\srename(?:\s(.*))?$")]
        [Priority(-31)]
        private async Task CmdRenameAsync(CommandContext context, string queueName, string args = null, CancellationToken cancellationToken = default)
        {
            string newName = args?.Trim();
            // check forbidden name
            if (IsQueueNameForbidden(newName))
            {
                await context.ReplyTextAsync($"(n) Queue name \"{newName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return;
            }

            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await context.ReplyTextAsync("(n) To rename a queue, you need to be its owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
            }

            // check same name
            if (newName.Equals(queue.Name, StringComparison.OrdinalIgnoreCase))
            {
                await context.ReplyTextAsync($"(n) Queue is already named \"{newName}\".", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check new queue name doesn't yet exist
            IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(newName, cancellationToken).ConfigureAwait(false);
            if (existingQueue != null)
            {
                await context.ReplyTextAsync($"(n) Queue \"{existingQueue.Name}\" already exists.", cancellationToken).ConfigureAwait(false);
                return;
            }

            queue.Name = newName;
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
            _idQueueStore.FlushBatch();
            await context.ReplyTextAsync($"(y) Queue renamed to \"{newName}\".", cancellationToken).ConfigureAwait(false);
        }

        /* TRANSFER */
        [RegexCommand(@"^(.+)?\squeue\stransfer(?:\s(.*))?$")]
        [Priority(-31)]
        private async Task CmdTransferAsync(CommandContext context, string queueName, 
            [MissingError("(n) Please provide ID of the user to transfer the queue to.")][ConvertingError("(n) `{{Arg}}` is not a valid user ID.")] uint newOwnerID, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (queue.OwnerID != null && !await IsQueueOwnerOrBotAdmin(queue, context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await context.ReplyTextAsync("(n) To transfer a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                return;
            }

            WolfUser user = await context.Client.GetUserAsync(newOwnerID, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                await context.ReplyTextAsync($"(n) User {newOwnerID} not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            IdQueue userCurrentQueue = await _idQueueStore.GetIdQueueByOwnerAsync(user.ID, cancellationToken).ConfigureAwait(false);
            if (userCurrentQueue != null)
            {
                await context.ReplyTextAsync($"(n) User {user.Nickname} already owns a queue. One user can only own one queue.", cancellationToken).ConfigureAwait(false);
                return;
            }

            queue.OwnerID = user.ID;
            await SaveQueueAsync(context, queue, cancellationToken).ConfigureAwait(false);
            _idQueueStore.FlushBatch();
            await context.ReplyTextAsync($"(y) Queue `{queue.Name}` transferred to {user.Nickname}.", cancellationToken).ConfigureAwait(false);
        }

        /* INFO */
        [RegexCommand(@"^(.+)?\squeue\sinfo")]
        [Priority(-31)]
        private async Task CmdInfoAsync(CommandContext context, string queueName, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetQueueAsync(context, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
            {
                if (queueName.Equals("my", StringComparison.OrdinalIgnoreCase))
                    await context.ReplyTextAsync("(n) Your queue not found.", cancellationToken).ConfigureAwait(false);
                else
                    await context.ReplyTextAsync($"(n) Queue {queueName} not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            WolfUser user = null;
            if (queue.OwnerID != null)
                user = await context.Client.GetUserAsync(queue.OwnerID.Value, cancellationToken).ConfigureAwait(false);

            await context.ReplyTextAsync(
                $"Name: {queue.Name}\r\n" +
                $"Owner ID: {(queue.OwnerID?.ToString() ?? "-")}\r\n" +
                $"Owner: {(user?.Nickname ?? "-")}\r\n" +
                $"ID Count: {queue.QueuedIDs?.Count ?? 0}\r\n" +
                $"\r\nID: {queue.ID}",
                cancellationToken).ConfigureAwait(false);
        }
        #endregion


        #region Helpers
        private async Task<bool> IsQueueOwnerOrBotAdmin(IdQueue queue, uint userID, CancellationToken cancellationToken = default)
        {
            // check if this is owner's queue
            if (queue.OwnerID != null && queue.OwnerID.Value == userID)
                return true;
            // if not, check if bot admin
            UserData user = await _userDataStore.GetUserDataAsync(userID, cancellationToken).ConfigureAwait(false);
            if (user.IsBotAdmin)
                return true;
            // if checks failed, is not owner or admin
            return false;
        }

        private (HashSet<uint> ids, HashSet<string> revisit) GetIDsFromArgs(string args)
        {
            string[] argsSplit = args.Split(_queuesOptions.IdSplitCharacters, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> needsRevisit = new HashSet<string>();
            HashSet<uint> ids = new HashSet<uint>(argsSplit.Length);
            for (int i = 0; i < argsSplit.Length; i++)
            {
                string idValue = argsSplit[i];
                if (uint.TryParse(idValue, out uint id))
                    ids.Add(id);
                else if (idValue.Any(char.IsDigit))
                    needsRevisit.Add(idValue);
            }

            return (ids, needsRevisit);
        }

        private async Task<bool> SaveQueueAsync(CommandContext context, IdQueue queue, CancellationToken cancellationToken = default)
        {
            try
            {
                await _idQueueStore.SetIdQueueAsync(queue, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving queue {QueueName} in the database", queue.Name))
            {
                if (context?.Message != null)
                    await context.ReplyTextAsync($"/alert Failed saving queue '{queue.Name}' in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private async Task<IdQueue> GetOrCreateQueueAsync(CommandContext context, string name, CancellationToken cancellationToken = default)
        {
            // first try to get existing queue
            IdQueue result = await GetQueueAsync(context, name, cancellationToken).ConfigureAwait(false);
            if (result != null)
                return result;

            // if not exist, resolve name for new queue
            bool claiming = false;
            string queueName = name;
            if (name.Equals("my", StringComparison.OrdinalIgnoreCase))
            {
                WolfUser user = await context.Client.GetUserAsync(context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                queueName = user.Nickname;
                claiming = true;
            }

            // check forbidden name
            if (IsQueueNameForbidden(queueName))
            {
                await context.ReplyTextAsync($"(n) Queue name \"{queueName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return null;
            }

            // if used "my", check if one with new name already exists
            if (claiming)
            {
                IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(queueName, cancellationToken).ConfigureAwait(false);
                if (existingQueue != null)
                {
                    if (existingQueue.OwnerID == null)
                        await context.ReplyTextAsync( $"(n) Queue \"{existingQueue.Name}\" already exists, but is not claimed by you.\r\n" +
                            $"Use '{context.Options.Prefix}{queueName}queue claim' to set as yours!", cancellationToken).ConfigureAwait(false);
                    else
                    {
                        WolfUser queueOwner = await context.Client.GetUserAsync(existingQueue.OwnerID.Value, cancellationToken).ConfigureAwait(false);
                        await context.ReplyTextAsync($"(n) Queue \"{existingQueue.Name}\" already exists, but is claimed by {queueOwner.Nickname}. :(", cancellationToken).ConfigureAwait(false);
                    }
                    return null;
                }
            }

            // if all checks succeeded, we can create a new one
            result = new IdQueue(queueName);
            if (claiming)
                result.OwnerID = context.Message.SenderID.Value;
            return result;
        }

        private async Task<IdQueue> GetQueueAsync(CommandContext context, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (name.Equals("my", StringComparison.OrdinalIgnoreCase))
                    return await _idQueueStore.GetIdQueueByOwnerAsync(context.Message.SenderID.Value, cancellationToken);
                else
                    return await _idQueueStore.GetIdQueueByNameAsync(name, cancellationToken);
            }
            catch (Exception)
            {
                await context.ReplyTextAsync("/alert Failed retrieving queue from database.", cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private bool IsQueueNameForbidden(string queueName)
            => string.IsNullOrWhiteSpace(queueName) || _queuesOptions.ForbiddenQueueNames.Contains(queueName);
        #endregion
    }
}
