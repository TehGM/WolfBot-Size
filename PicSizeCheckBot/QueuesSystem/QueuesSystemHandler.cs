using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    public class QueuesSystemHandler : IHostedService
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<QueuesSystemOptions> _queuesOptions;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IIdQueueStore _idQueueStore;
        private readonly IUserDataStore _userDataStore;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;
        private readonly Regex _queueCommandRegex = new Regex(@"^(.+)?\squeue(?:\s([A-Za-z]+))?(?:\s(.+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public QueuesSystemHandler(IHostedWolfClient client, IIdQueueStore idQueueStore, IUserDataStore userDataStore,
            IOptionsMonitor<QueuesSystemOptions> queuesOptions, IOptionsMonitor<BotOptions> botOptions, ILogger<QueuesSystemHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._queuesOptions = queuesOptions;
            this._client = client;
            this._idQueueStore = idQueueStore;
            this._userDataStore = userDataStore;

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

                CancellationToken cancellationToken = _cts?.Token ?? default;
                if (command.StartsWith("queue help", StringComparison.OrdinalIgnoreCase) || command.StartsWith("queues help", StringComparison.OrdinalIgnoreCase))
                {
                    await CmdHelpAsync(message, cancellationToken).ConfigureAwait(false);
                }

                Match match = _queueCommandRegex.Match(command);
                if (!match.Success)
                    return;

                string queueName = match.Groups[1].Value.Trim();
                string commandSwitch = match.Groups[2]?.Value?.ToLowerInvariant().Trim();
                string args = match.Groups[3].Value.Trim();

                Func<ChatMessage, string, string, CancellationToken, Task> cmdMethod = null;

                switch (commandSwitch)
                {
                    case null when (string.IsNullOrWhiteSpace(args)):
                    case "" when (string.IsNullOrWhiteSpace(args)):
                    case "next":
                        cmdMethod = CmdNextAsync;
                        break;
                    case "clear":
                        cmdMethod = CmdClearAsync;
                        break;
                    case "rename":
                        cmdMethod = CmdRenameAsync;
                        break;
                    case "info":
                        cmdMethod = CmdInfoAsync;
                        break;
                    case "show":
                        cmdMethod = CmdShowAsync;
                        break;
                    case "remove":
                        cmdMethod = CmdRemoveAsync;
                        break;
                    case "transfer":
                        cmdMethod = CmdTransferAsync;
                        break;
                    default:
                        cmdMethod = CmdAddAsync;
                        break;
                }

                await cmdMethod(message, queueName, args, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (MessageSendingException ex) when (ex.SentMessage is ChatMessage && ex.Response is WolfResponse response && response.ErrorCode == WolfErrorCode.LoginIncorrectOrCannotSendToGroup) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        #region Commands
        /* HELP */
        private async Task CmdHelpAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            WolfUser owner = await _client.GetUserAsync(_botOptions.CurrentValue.OwnerID, cancellationToken).ConfigureAwait(false);
            await _client.ReplyTextAsync(message, string.Format(@"Queue commands:
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
_botOptions.CurrentValue.CommandPrefix, owner.Nickname, owner.ID),
cancellationToken).ConfigureAwait(false);
        }

        /* NEXT */
        private async Task CmdNextAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            if (message.IsPrivateMessage)
            {
                await _client.ReplyTextAsync(message, $"(n) This command can only be used in groups.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get queue and ensure not empty
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name
            if (!queue.QueuedIDs.Any())
            {
                await _client.ReplyTextAsync(message, $"Queue {queue.Name} is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            uint gameID = queue.QueuedIDs.Dequeue();
            await _client.ReplyTextAsync(message, BotInteractionUtilities.GetSubmissionBotShowCommand(_botOptions.CurrentValue, gameID), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* ADD */
        private async Task CmdAddAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
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
            await _client.ReplyTextAsync(message, builder.ToString(), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* SHOW */
        private async Task CmdShowAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            if (queue.QueuedIDs?.Any() != true)
            {
                await _client.ReplyTextAsync(message, $"{queue.Name} is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            bool plural = queue.QueuedIDs.Count > 1;
            await _client.ReplyTextAsync(message, $"Currently there {(plural ? "are" : "is")} {queue.QueuedIDs.Count} ID{(plural ? "s" : "")} on {queue.Name} queue:\r\n{string.Join(", ", queue.QueuedIDs)}");
        }

        /* REMOVE */
        private async Task CmdRemoveAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) To remove from a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
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
            await _client.ReplyTextAsync(message, builder.ToString(), cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* CLEAR */
        private async Task CmdClearAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) To clear a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
            }

            int idCount = queue.QueuedIDs.Count;
            queue.QueuedIDs.Clear();
            await _client.ReplyTextAsync(message, $"(y) {idCount} ID{(idCount > 1 ? "s" : "")} removed from queue \"{queue.Name}\" .", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* RENAME */
        private async Task CmdRenameAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) To rename a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
            }

            string newName = args.Trim();

            // check same name
            if (newName.Equals(queue.Name, StringComparison.OrdinalIgnoreCase))
            {
                await _client.ReplyTextAsync(message, $"(n) Queue is already named \"{newName}\".", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check forbidden name
            if (IsQueueNameForbidden(newName))
            {
                await _client.ReplyTextAsync(message, $"(n) Queue name \"{newName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check new queue name doesn't yet exist
            IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(newName, cancellationToken).ConfigureAwait(false);
            if (existingQueue != null)
            {
                await _client.ReplyTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists.", cancellationToken).ConfigureAwait(false);
                return;
            }

            queue.Name = newName;
            await _client.ReplyTextAsync(message, $"(y) Queue renamed to \"{newName}\".", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* ASSIGN */
        private async Task CmdTransferAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue, or user is bot admin
            if (!await IsQueueOwnerOrBotAdmin(queue, message.SenderID.Value, cancellationToken).ConfigureAwait(false))
            {
                await _client.ReplyTextAsync(message, "(n) To transfer a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!uint.TryParse(args, out uint id))
            {
                await _client.ReplyTextAsync(message, $"(n) `{args}` is not a valid user ID.", cancellationToken).ConfigureAwait(false);
                return;
            }

            WolfUser user = await _client.GetUserAsync(id, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                await _client.ReplyTextAsync(message, $"(n) User {args} not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            queue.OwnerID = user.ID;
            await _client.ReplyTextAsync(message, $"(y) Queue `{queue.Name}` transferred to {user.Name}.", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* INFO */
        private async Task CmdInfoAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);

            if (queue == null)
            {
                if (queueName.Equals("my", StringComparison.OrdinalIgnoreCase))
                    await _client.ReplyTextAsync(message, "(n) Your queue not found.", cancellationToken).ConfigureAwait(false);
                else
                    await _client.ReplyTextAsync(message, $"(n) Queue {queueName} not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            WolfUser user = null;
            if (queue.OwnerID != null)
                user = await _client.GetUserAsync(queue.OwnerID.Value, cancellationToken).ConfigureAwait(false);

            await _client.ReplyTextAsync(message,
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
            string[] argsSplit = args.Split(_queuesOptions.CurrentValue.IdSplitCharacters, StringSplitOptions.RemoveEmptyEntries);
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

        private async Task<bool> SaveQueueAsync(ChatMessage message, IdQueue queue, CancellationToken cancellationToken = default)
        {
            try
            {
                await _idQueueStore.SetIdQueueAsync(queue, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving queue {QueueName} in the database", queue.Name))
            {
                if (message != null)
                    await _client.ReplyTextAsync(message, $"/alert Failed saving queue '{queue.Name}' in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private async Task<IdQueue> GetOrCreateQueueAsync(ChatMessage message, string name, CancellationToken cancellationToken = default)
        {
            // first try to get existing queue
            IdQueue result = await GetQueueAsync(message, name, cancellationToken).ConfigureAwait(false);
            if (result != null)
                return result;

            // if not exist, resolve name for new queue
            bool claiming = false;
            string queueName = name;
            if (name.Equals("my", StringComparison.OrdinalIgnoreCase))
            {
                WolfUser user = await _client.GetUserAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                queueName = user.Nickname;
                claiming = true;
            }

            // check forbidden name
            if (IsQueueNameForbidden(queueName))
            {
                await _client.ReplyTextAsync(message, $"(n) Queue name \"{queueName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return null;
            }

            // if used "my", check if one with new name already exists
            if (claiming)
            {
                IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(queueName, cancellationToken).ConfigureAwait(false);
                if (existingQueue != null)
                {
                    if (existingQueue.OwnerID == null)
                        await _client.ReplyTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists, but is not claimed by you.\r\n" +
                            $"Use '{_botOptions.CurrentValue.CommandPrefix}{queueName}queue claim' to set as yours!", cancellationToken).ConfigureAwait(false);
                    else
                    {
                        WolfUser queueOwner = await _client.GetUserAsync(existingQueue.OwnerID.Value, cancellationToken).ConfigureAwait(false);
                        await _client.ReplyTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists, but is claimed by {queueOwner.Nickname}. :(", cancellationToken).ConfigureAwait(false);
                    }
                    return null;
                }
            }

            // if all checks succeeded, we can create a new one
            result = new IdQueue(queueName);
            if (claiming)
                result.OwnerID = message.SenderID.Value;
            return result;
        }

        private async Task<IdQueue> GetQueueAsync(ChatMessage message, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (name.Equals("my", StringComparison.OrdinalIgnoreCase))
                    return await _idQueueStore.GetIdQueueByOwnerAsync(message.SenderID.Value, cancellationToken);
                else
                    return await _idQueueStore.GetIdQueueByNameAsync(name, cancellationToken);
            }
            catch (Exception)
            {
                await _client.ReplyTextAsync(message, "/alert Failed retrieving queue from database.", cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private bool IsQueueNameForbidden(string queueName)
            => _queuesOptions.CurrentValue.ForbiddenQueueNames.Contains(queueName);
        #endregion


        #region Interface implementations
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
        #endregion
    }
}
