using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Utilities;

namespace TehGM.WolfBots.PicSizeCheckBot.UserNotes
{
    [CommandsHandler]
    public class UserNotesHandler
    {
        private readonly BotOptions _botOptions;
        private readonly UserNotesOptions _notesOptions;
        private readonly IUserDataStore _userDataStore;
        private readonly ILogger _log;

        public UserNotesHandler(IUserDataStore userDataStore, IOptionsSnapshot<BotOptions> botOptions, IOptionsSnapshot<UserNotesOptions> notesOptions, ILogger<UserNotesHandler> logger)
        {
            // store all services
            this._log = logger;
            this._botOptions = botOptions.Value;
            this._notesOptions = notesOptions.Value;
            this._userDataStore = userDataStore;
        }


        #region Commands
        /* HELP */
        [Command("notes help")]
        private async Task CmdHelpAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            WolfUser owner = await context.Client.GetUserAsync(_botOptions.OwnerID, cancellationToken).ConfigureAwait(false);
            await context.ReplyTextAsync(string.Format(@"Notes commands:
`{0}notes` - get list of your notes
`{0}notes <ID>` - get a specific note
`{0}notes add <text>` - adds a new note
`{0}notes remove <ID>` - delete a specific note
`{0}notes clear` - removes all your notes

For bug reports or suggestions, contact {1} (ID: {2})",
context.Options.Prefix, owner.Nickname, owner.ID),
cancellationToken).ConfigureAwait(false);
        }

        /* GET */
        [RegexCommand(@"^notes?(?:\s(.+))?")]
        [Priority(-41)]
        private async Task CmdGetAsync(CommandContext context, string userInput = null, CancellationToken cancellationToken = default)
        {
            // if no ID provided, get all
            if (string.IsNullOrWhiteSpace(userInput))
            {
                UserData data = await GetOrCreateUserData(context, cancellationToken).ConfigureAwait(false);
                if (data?.Notes?.Any() != true)
                {
                    await context.ReplyTextAsync("Your notes list is empty.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                // concat all
                int maxMsgLength = _notesOptions.MaxMessageLength;
                int maxNoteLength = _notesOptions.MaxLengthPerBulkNote;
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

                await context.ReplyTextAsync(string.Join("\r\n", includedEntries), cancellationToken).ConfigureAwait(false);
            }
            // otherwise, get a specific one
            else
            {
                if (!uint.TryParse(userInput, out uint id))
                {
                    await context.ReplyTextAsync($"(n) `{userInput}` is not a valid note ID.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                UserData data = await GetOrCreateUserData(context, cancellationToken).ConfigureAwait(false);
                if (!data.Notes.TryGetValue(id, out string note))
                {
                    await context.ReplyTextAsync($"(n) You don't have a note with ID {id}.", cancellationToken).ConfigureAwait(false);
                    return;
                }

                await context.ReplyTextAsync(note, cancellationToken).ConfigureAwait(false);
            }
        }

        
        /* ADD */
        [RegexCommand(@"^notes?\sadd(?:\s(.+))?")]
        private async Task CmdAddAsync(CommandContext context, string userInput = null, CancellationToken cancellationToken = default)
        {
            // validate not empty
            string note = userInput.Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                await context.ReplyTextAsync($"(n) Cannot add an empty note.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // validate not too long
            if (note.Length > _notesOptions.MaxNoteLength)
            {
                await context.ReplyTextAsync($"(n) User notes can have maximum {_notesOptions.MaxNoteLength} characters. Your note has {note.Length} characters.", cancellationToken).ConfigureAwait(false);
                return;
            }

            // validate not at max already and get next free ID slot at once
            UserData data = await GetOrCreateUserData(context, cancellationToken).ConfigureAwait(false);
            uint id = default;
            for (uint i = 1; i <= _notesOptions.MaxNotesCount; i++)
            {
                if (!data.Notes.ContainsKey(i))
                {
                    id = i;
                    break;
                }
                if (i == _notesOptions.MaxNotesCount)
                {
                    await context.ReplyTextAsync($"(n) You reached limit of {_notesOptions.MaxNotesCount} notes per user.", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // insert note and save data
            data.Notes.Add(id, note);
            await context.ReplyTextAsync($"(y) Your note added at position {id}.", cancellationToken).ConfigureAwait(false);
            await SaveUserDataAsync(context, data, cancellationToken).ConfigureAwait(false);
        }


        /* REMOVE */
        [RegexCommand(@"^notes?\s(?:del|delete|remove)(?:\s(.+))?")]
        private async Task CmdRemoveAsync(CommandContext context,
             [MissingError("(n) Please provide a valid note ID.")] [ConvertingError("(n) `{{Arg}}` is not a valid note ID.")] uint noteID, CancellationToken cancellationToken = default)
        {
            UserData data = await GetOrCreateUserData(context, cancellationToken).ConfigureAwait(false);
            if (!data.Notes.Remove(noteID))
            {
                await context.ReplyTextAsync($"(n) You don't have a note with ID {noteID}.", cancellationToken).ConfigureAwait(false);
                return;
            }

            await context.ReplyTextAsync($"(y) Note {noteID} removed.", cancellationToken).ConfigureAwait(false);
            await SaveUserDataAsync(context, data, cancellationToken).ConfigureAwait(false);
        }


        /* CLEAR */
        [RegexCommand("notes clear")]
        private async Task CmdClearAsync(CommandContext context, CancellationToken cancellationToken = default)
        {
            UserData data = await GetOrCreateUserData(context, cancellationToken).ConfigureAwait(false);
            bool needsSaving = data.Notes.Any();
            data.Notes.Clear();
            await context.ReplyTextAsync($"(y) Your notes list cleared.", cancellationToken).ConfigureAwait(false);
            if (needsSaving)
                await SaveUserDataAsync(context, data, cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Helpers
        private async Task<UserData> GetOrCreateUserData(CommandContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                UserData result = await _userDataStore.GetUserDataAsync(context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false);
                return result ?? new UserData(context.Message.SenderID.Value);
            }
            catch
            {
                await context.ReplyTextAsync("/alert Failed retrieving user data from the database.", cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task<bool> SaveUserDataAsync(CommandContext context, UserData data, CancellationToken cancellationToken = default)
        {
            try
            {
                await _userDataStore.SetUserDataAsync(data, false, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex.LogAsError(_log, "Failed saving notes for user {UserID} in the database", data.ID))
            {
                await context.ReplyTextAsync("/alert Failed saving notes in the database.", cancellationToken).ConfigureAwait(false);
                return false;
            }
        }
        #endregion
    }
}
