using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace TehGM.WolfBots.PicSizeCheckBot.UserNotes
{
    public class UserNotesHandler : IHostedService, IDisposable
    {
        private readonly IHostedWolfClient _client;
        private readonly IOptionsMonitor<BotOptions> _botOptions;
        private readonly IOptionsMonitor<UserNotesOptions> _notesOptions;
        private readonly IUserDataStore _userDataStore;
        private readonly ILogger _log;

        private CancellationTokenSource _cts;
        private readonly Regex _addCommandRegex = new Regex(@"^notes?\sadd(?:\s(.+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly Regex _removeCommandRegex = new Regex(@"^notes?\s(?:del|delete|remove)(?:\s(.+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly Regex _clearCommandRegex = new Regex(@"^notes?\sclear", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly Regex _getCommandRegex = new Regex(@"^notes?(?:\s(.+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly Regex _helpCommandRegex = new Regex(@"^notes?\shelp", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public UserNotesHandler(IHostedWolfClient client, IUserDataStore userDataStore,
            IOptionsMonitor<BotOptions> botOptions, IOptionsMonitor<UserNotesOptions> notesOptions, ILogger<UserNotesHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions;
            this._notesOptions = notesOptions;
            this._client = client;
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

                if (_helpCommandRegex.TryGetMatch(command, out _))
                    await CmdHelpAsync(message, cancellationToken).ConfigureAwait(false);
                else if (_clearCommandRegex.TryGetMatch(command, out _))
                    await CmdClearAsync(message, cancellationToken).ConfigureAwait(false);
                else if (_addCommandRegex.TryGetMatch(command, out Match addMatch))
                    await CmdAddAsync(message, addMatch.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
                else if (_removeCommandRegex.TryGetMatch(command, out Match removeMatch))
                    await CmdRemoveAsync(message, removeMatch.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
                // get should come last!!
                else if (_getCommandRegex.TryGetMatch(command, out Match getMatach))
                    await CmdGetAsync(message, getMatach.Groups[1]?.Value, cancellationToken).ConfigureAwait(false);
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
            await _client.ReplyTextAsync(message, string.Format(@"Notes commands:
`{0}notes` - get list of your notes
`{0}notes <ID>` - get a specific note
`{0}notes add <text>` - adds a new note
`{0}notes remove <ID>` - delete a specific note
`{0}notes clear` - removes all your notes

For bug reports or suggestions, contact {1} (ID: {2})",
_botOptions.CurrentValue.CommandPrefix, owner.Nickname, owner.ID),
cancellationToken).ConfigureAwait(false);
        }

        /* GET */
        private async Task CmdGetAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            // if no ID provided, get all
            if (string.IsNullOrWhiteSpace(userInput))
            {
                UserData data = await GetOrCreateUserData(message, cancellationToken).ConfigureAwait(false);
                if (data?.Notes?.Any() != true)
                {
                    await _client.ReplyTextAsync(message, "Your notes list is empty.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                // concat all
                int maxMsgLength = _notesOptions.CurrentValue.MaxMessageLength;
                int maxNoteLength = _notesOptions.CurrentValue.MaxLengthPerBulkNote;
                List<string> entries = new List<string>(data.Notes.Select(pair => $"{pair.Key}: {pair.Value}"));
                // keep list of entries, so can remove last one if adding "not all fit message" requires that
                List<string> includedEntries = new List<string>(data.Notes.Count);
                int includedLength = -("\r\n".Length);  // start with negative, as one entry won't have new line separator
                for (int i = 0; i < entries.Count; i++)
                {
                    string e = entries[i];
                    if (includedLength + e.Length + "\r\n".Length <= maxMsgLength)
                    {
                        string include = e;
                        if (e.Length > maxNoteLength)
                            include = e.Remove(maxNoteLength).Trim() + "...";

                        includedEntries.Add(include);
                        includedLength += include.Length;
                    }
                    else break;
                }

                // if not all fit, tell user about it
                if (entries.Count > includedEntries.Count)
                {
                    string appendNotif = $"... and {entries.Count - includedEntries.Count} that did not fit. :(";
                    // if message does not fit, need to remove last included message :(
                    if (includedLength + appendNotif.Length + "\r\n".Length > maxMsgLength)
                        includedEntries.RemoveAt(includedEntries.Count - 1);
                    includedEntries.Add(appendNotif);
                }

                await _client.ReplyTextAsync(message, string.Join("\r\n", includedEntries), cancellationToken).ConfigureAwait(false);
            }
            // otherwise, get a specific one
            else
            {
                if (!uint.TryParse(userInput, out uint id))
                {
                    await _client.ReplyTextAsync(message, $"(n) `{userInput}` is not a valid note ID.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                UserData data = await GetOrCreateUserData(message, cancellationToken).ConfigureAwait(false);
                if (!data.Notes.TryGetValue(id, out string note))
                {
                    await _client.ReplyTextAsync(message, $"(n) You don't have a note with ID {id}.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                await _client.ReplyTextAsync(message, note, cancellationToken).ConfigureAwait(false);
            }
        }

        
        /* ADD */
        private async Task CmdAddAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            // validate not empty
            string note = userInput.Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                await _client.ReplyTextAsync(message, $"(n) Cannot add an empty note.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // validate not too long
            UserNotesOptions options = _notesOptions.CurrentValue;
            if (note.Length > options.MaxNoteLength)
            {
                await _client.ReplyTextAsync(message, $"(n) User notes can have maximum {options.MaxNoteLength} characters. Your note has {note.Length} characters.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // validate not at max already and get next free ID slot at once
            UserData data = await GetOrCreateUserData(message, cancellationToken).ConfigureAwait(false);
            uint id = default;
            for (uint i = 1; i <= options.MaxNotesCount; i++)
            {
                if (!data.Notes.ContainsKey(i))
                {
                    id = i;
                    break;
                }
                if (i == options.MaxNotesCount)
                {
                    await _client.ReplyTextAsync(message, $"(n) You reached limit of {options.MaxNotesCount} notes per user.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // insert note and save data
            data.Notes.Add(id, note);
            await _client.ReplyTextAsync(message, $"(y) Your note added at position {id}.", cancellationToken).ConfigureAwait(false);
            await SaveUserDataAsync(message, data, cancellationToken).ConfigureAwait(false);
        }


        /* REMOVE */
        private async Task CmdRemoveAsync(ChatMessage message, string userInput, CancellationToken cancellationToken = default)
        {
            if (!uint.TryParse(userInput, out uint id))
            {
                await _client.ReplyTextAsync(message, $"(n) `{userInput}` is not a valid note ID.", cancellationToken).ConfigureAwait(false);
                return;
            }

            UserData data = await GetOrCreateUserData(message, cancellationToken).ConfigureAwait(false);
            if (!data.Notes.Remove(id))
            {
                await _client.ReplyTextAsync(message, $"(n) You don't have a note with ID {id}.", cancellationToken).ConfigureAwait(false);
                return;
            }

            await _client.ReplyTextAsync(message, $"(y) Note {id} removed.", cancellationToken).ConfigureAwait(false);
            await SaveUserDataAsync(message, data, cancellationToken).ConfigureAwait(false);
        }


        /* CLEAR */
        private async Task CmdClearAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            UserData data = await GetOrCreateUserData(message, cancellationToken).ConfigureAwait(false);
            bool needsSaving = data.Notes.Any();
            data.Notes.Clear();
            await _client.ReplyTextAsync(message, $"(y) Your notes list cleared.", cancellationToken).ConfigureAwait(false);
            if (needsSaving)
                await SaveUserDataAsync(message, data, cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Helpers
        private async Task<UserData> GetOrCreateUserData(ChatMessage message, CancellationToken cancellationToken = default)
        {
            try
            {
                UserData result = await _userDataStore.GetUserDataAsync(message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                return result ?? new UserData(message.SenderID.Value);
            }
            catch
            {
                await _client.ReplyTextAsync(message, "/alert Failed retrieving user data from the database.", cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task<bool> SaveUserDataAsync(ChatMessage message, UserData data, CancellationToken cancellationToken = default)
        {
            try
            {
                await _userDataStore.SetUserDataAsync(data, false, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving notes for user {UserID} in the database", data.ID))
            {
                await _client.ReplyTextAsync(message, "/alert Failed saving notes in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
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
