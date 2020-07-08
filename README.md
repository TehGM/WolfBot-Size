# WOLF Pic Size Bot
A Pic Size Bot for WOLF, designed to help checking sizes of posted images for bot content editors.

Uses [Wolfringo library](https://github.com/TehGM/Wolfringo) for connection and MongoDB (using [C# MongoDB Driver](https://docs.mongodb.com/drivers/csharp)) for storage.

## Features
You can use `!size help` to get brief list of common commands. All commands with more detailed descriptions are listed on [Commands](https://github.com/TehGM/WolfBot-Size/wiki/Commands) wiki page.

All commands need to have a prefix (currently `!size `) when used in a group. Commands executed in a private message can skip the prefix.

### Size checking
The core feature of the bot. The bot will automatically post sizes of posted images. The bot can also check size of image from URL, using `check <url>` command.

The size checking settings can be changed using `listen`, `posturl` and `enable`/`disable` commands.

### Next ID
The bot makes it easier to pull next game ID with `next` command. `next continue` and `next update` commands also help with storing the last ID in Autopost bot.

The bot automatically uses [known IDs list](PicSizeCheckBot/guesswhat-ids.json) to skip IDs that do not exist. If using `next` command with IDs that are greater than last entry on the list, bot will pull IDs one by one.

### Queues System
Queues allow to store IDs in order for later in a categorized manner. You can use queues using `<QueueName> queue` commands.

If you use `my` as `<QueueName>`, the bot will use a queue tied with your user ID. If you don't have a queue, bot will automatically create one using your current display name. Anyone can see and add to your queue, and you can then access it using simple `my queue` command.

Queue command is relatively complex, so make sure to see [Commands](https://github.com/TehGM/WolfBot-Size/wiki/Commands#queues-system) on wiki.

### User Notes
Bot can store your notes - be it set of IDs, an important reminder or your shopping list. You can see excerpts of your notes with `notes` command. Each note has an ID, and you can access full note text using `notes <ID>` command.

To add to your notes, simply use `notes add <Text>`, and to remove use `notes remove <ID>`, or to remove all use `notes clear`.

Due to WOLF protocol limitations, notes cannot be very long.

### Mentions
The bot can message a user whenever their name is mentioned in a group chat. This feature uses regular expressions, which gives huge flexibility when setting triggers.

Each user can have more than one trigger messaging them.

## Development
This bot is under continuous (if sometimes slow) development. Breaking changes might be introduced at any time.

If you spot a bug or want to suggest a feature or improvement, feel free to open a new [Issue](https://github.com/TehGM/WolfBot-Size/issues).

## License
Copyright (c) 2020 TehGM

Licensed under [GNU Affero General Public License v3.0](LICENSE) (GNU AGPL-3.0).
