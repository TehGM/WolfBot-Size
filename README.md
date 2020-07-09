# WOLF Pic Size Bot
A Pic Size Bot for WOLF, designed to help checking sizes of posted images for bot content editors.

Uses [Wolfringo library](https://github.com/TehGM/Wolfringo) for connection and MongoDB (using [C# MongoDB Driver](https://docs.mongodb.com/drivers/csharp)) for storage.

## Features
View bot features information on [Wiki page](https://github.com/TehGM/WolfBot-Size/wiki#features).

All commands with more detailed descriptions are listed on [Commands wiki page](https://github.com/TehGM/WolfBot-Size/wiki/Commands).

All commands need to have a prefix (currently `!size `) when used in a group. Commands executed in a private message can skip the prefix.

## Running locally
1. Clone this repository to get all files.
2. Set up MongoDB database with following collections: `GroupConfigs`, `IdQueues`, `Mentions` and `UsersData`.
3. Create `appsecrets.json` file. See [example file](PicSizeCheckBot/appsecrets-example.json) for example structure.  
This file will hold secrets, so it should not be included in source control repository. `.gitignore` file included with this repo will ignore `appsecrets.json` and `appsecrets.*.json` files.
4. Populate secrets file with bot login credentials and MongoDB connection string for your DB.
5. *(optional)* If using DataDog for logs, create a following section in `appsecrets.json`, replacying `<api-key>` with your DataDog application API key:  
```json
"Serilog": {
  "DataDog": {
    "ApiKey": "<api-key>"
  }
}
```
6. Build and run `PicSizeCheckBot` project.


## Development
This bot is under continuous (if sometimes slow) development. Breaking changes might be introduced at any time.

If you spot a bug or want to suggest a feature or improvement, feel free to open a new [Issue](https://github.com/TehGM/WolfBot-Size/issues).

## License
Copyright (c) 2020 TehGM

Licensed under [GNU Affero General Public License v3.0](LICENSE) (GNU AGPL-3.0).
