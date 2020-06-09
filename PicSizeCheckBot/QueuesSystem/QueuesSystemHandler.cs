﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
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
                    default:
                        cmdMethod = CmdAddAsync;
                        break;
                }

                await cmdMethod(message, queueName, args, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) when (ex.LogAsError(_log, "Error occured when processing message")) { }
        }

        #region Commands
        /* HELP */
        private async Task CmdHelpAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            WolfUser owner = await _client.GetUserAsync(_botOptions.CurrentValue.OwnerID, cancellationToken).ConfigureAwait(false);
            await _client.RespondWithTextAsync(message, string.Format(@"Queue commands:
`{0}<queue name> queue next` - pulls the next ID from <queue name>
`{0}<queue name> queue add <IDs>` - adds IDs to the queue
`{0}<queue name> queue show` - shows all IDs on the queue
`{0}<queue name> queue remove <IDs>` - removes selected IDs from the queue
`{0}<queue name> queue clear` - removes all IDs from the queue
`{0}<queue name> queue rename <new name>` - changes name of the queue to <new name>
`{0}<queue name> queue claim` - claims the queue, so you can use ""my"" as it's name
`{0}<queue name> queue transfer <user ID>` - transfers ownership of queue to user with specified ID
`{0}<queue name> info` - shows info about queue

`clear`, `rename` and `transfer` can only be used if you own the queue.
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
                await _client.RespondWithTextAsync(message, $"(n) This command can only be used in groups.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // get queue and ensure not empty
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name
            if (!queue.QueuedIDs.Any())
            {
                await _client.RespondWithTextAsync(message, $"Queue {queue.Name} is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            uint gameID = queue.QueuedIDs.Dequeue();
            await SendShowCommandAsync(message.RecipientID, gameID, cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* ADD */
        private async Task CmdAddAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            string[] argsSplit = args.Split(_queuesOptions.CurrentValue.IdSplitCharacters, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> needsRevisit = new HashSet<string>();
            int addedCount = 0;
            int skippedCount = 0;
            for (int i = 0; i < argsSplit.Length; i++)
            {
                string idValue = argsSplit[i];
                if (uint.TryParse(idValue, out uint id))
                {
                    if (queue.QueuedIDs.Contains(id))
                        skippedCount++;
                    else
                    {
                        queue.QueuedIDs.Enqueue(id);
                        addedCount++;
                    }
                }
                else if (idValue.Any(char.IsDigit))
                    needsRevisit.Add(idValue);
            }

            if (!await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false))
                return;

            StringBuilder builder = new StringBuilder();
            if (addedCount > 0)
                builder.AppendFormat("(y) Added {0} IDs to {1} queue.", addedCount.ToString(), queue.Name);
            else
                builder.AppendFormat("(n) No ID added to {0} queue.", queue.Name);
            if (skippedCount > 0)
                builder.AppendFormat("\r\n{0} IDs already exist on the queue so were skipped.", skippedCount.ToString());
            if (needsRevisit.Count > 0)
                builder.AppendFormat("\r\n(n) {0} values weren't valid IDs, but contain digits - please revisit: {1}", needsRevisit.Count.ToString(), string.Join(", ", needsRevisit));

            await _client.RespondWithTextAsync(message, builder.ToString(), cancellationToken).ConfigureAwait(false);
        }

        /* SHOW */
        private async Task CmdShowAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            if (queue.QueuedIDs?.Any() == true)
            {
                await _client.RespondWithTextAsync(message, $"{queue.Name} is empty.", cancellationToken).ConfigureAwait(false);
                return;
            }

            bool plural = queue.QueuedIDs.Count > 1;
            await _client.RespondWithTextAsync(message, $"Currently there {(plural ? "are" : "is")} {queue.QueuedIDs.Count} ID{(plural ? "s" : "")} on {queueName} queue:\r\n{string.Join(", ", queue.QueuedIDs)}");
        }

        /* REMOVE */
        private async Task CmdRemoveAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {

        }

        /* CLEAR */
        private async Task CmdClearAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue
            if (queue.OwnerID == null || queue.OwnerID.Value != message.SenderID.Value)
            {
                // if not, check if bot admin
                UserData user = await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                if (user.IsBotAdmin)
                {
                    await _client.RespondWithTextAsync(message, "(n) To clear a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            int idCount = queue.QueuedIDs.Count;
            queue.QueuedIDs.Clear();
            await _client.RespondWithTextAsync(message, $"(y) {idCount} ID{(idCount > 1 ? "s" : "")} removed from queue \"{queue.Name}\" .", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* RENAME */
        private async Task CmdRenameAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetOrCreateQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);
            if (queue == null)
                return;         // if null, it means it's a forbidden name

            // check if this is owner's queue
            if (queue.OwnerID == null || queue.OwnerID.Value != message.SenderID.Value)
            {
                // if not, check if bot admin
                UserData user = await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                if (user.IsBotAdmin)
                {
                    await _client.RespondWithTextAsync(message, "(n) To rename a queue, you need to be it's owner or a bot admin.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            string newName = args.Trim();

            // check same name
            if (newName.Equals(queue.Name, StringComparison.OrdinalIgnoreCase))
            {
                await _client.RespondWithTextAsync(message, $"(n) Queue is already named \"{newName}\".", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check forbidden name
            if (IsQueueNameForbidden(newName))
            {
                await _client.RespondWithTextAsync(message, $"(n) Queue name \"{newName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // check new queue name doesn't yet exist
            IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(newName, cancellationToken).ConfigureAwait(false);
            if (existingQueue != null)
            {
                await _client.RespondWithTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists.", cancellationToken).ConfigureAwait(false);
                return;
            }

            queue.Name = newName;
            await _client.RespondWithTextAsync(message, $"(y) Queue renamed to \"{newName}\".", cancellationToken).ConfigureAwait(false);
            await SaveQueueAsync(message, queue, cancellationToken).ConfigureAwait(false);
        }

        /* ASSIGN */
        private async Task CmdTransferAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {

        }

        /* INFO */
        private async Task CmdInfoAsync(ChatMessage message, string queueName, string args, CancellationToken cancellationToken = default)
        {
            IdQueue queue = await GetQueueAsync(message, queueName, cancellationToken).ConfigureAwait(false);

            if (queue == null)
            {
                if (queueName.Equals("my", StringComparison.OrdinalIgnoreCase))
                    await _client.RespondWithTextAsync(message, "(n) Your queue not found.", cancellationToken).ConfigureAwait(false);
                else
                    await _client.RespondWithTextAsync(message, $"(n) Queue {queueName} not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            WolfUser user = null;
            if (queue.OwnerID != null)
                await _client.GetUserAsync(queue.OwnerID.Value, cancellationToken).ConfigureAwait(false);

            await _client.RespondWithTextAsync(message,
                $"Name: {queue.Name}\r\n" +
                $"Owner ID: {(queue.OwnerID != null ? queue.OwnerID.ToString() : "-")}\r\n" +
                $"Owner: {(user != null ? user.Nickname : "-")}\r\n" +
                $"ID Count: {queue.QueuedIDs?.Count ?? 0}\r\n" +
                $"\r\nID: {queue.ID}",
                cancellationToken).ConfigureAwait(false);
        }
        #endregion


        #region Helpers
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
                    await _client.RespondWithTextAsync(message, $"/alert Failed saving queue '{queue.Name}' in the database.", cancellationToken).ConfigureAwait(false);
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
                await _client.RespondWithTextAsync(message, $"(n) Queue name \"{queueName}\" is invalid or forbidden.", cancellationToken).ConfigureAwait(false);
                return null;
            }

            // if used "my", check if one with new name already exists
            if (claiming)
            {
                IdQueue existingQueue = await _idQueueStore.GetIdQueueByNameAsync(queueName, cancellationToken).ConfigureAwait(false);
                if (existingQueue != null)
                {
                    if (existingQueue.OwnerID == null)
                        await _client.RespondWithTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists, but is not claimed by you.\r\n" +
                            $"Use '{_botOptions.CurrentValue.CommandPrefix}{queueName}queue claim' to set as yours!", cancellationToken).ConfigureAwait(false);
                    else
                    {
                        WolfUser queueOwner = await _client.GetUserAsync(existingQueue.OwnerID.Value, cancellationToken).ConfigureAwait(false);
                        await _client.RespondWithTextAsync(message, $"(n) Queue \"{existingQueue.Name}\" already exists, but is claimed by {queueOwner.Nickname}. :(", cancellationToken).ConfigureAwait(false);
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
                await _client.RespondWithTextAsync(message, "/alert Failed retrieving queue from database.", cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private bool IsQueueNameForbidden(string queueName)
            => _queuesOptions.CurrentValue.ForbiddenQueueNames.Contains(queueName);

        private Task SendShowCommandAsync(uint groupID, uint gameID, CancellationToken cancellationToken = default)
        {
            StringBuilder builder = new StringBuilder(_queuesOptions.CurrentValue.SubmissionBotShowCommand);
            builder.Replace("{{id}}", gameID.ToString());
            return _client.SendGroupTextMessageAsync(groupID, builder.ToString(), cancellationToken);
        }
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
