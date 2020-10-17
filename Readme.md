## Yolol Ladder

This is a Discord bot providing helpful utilities and a code-golf competition for the [Yolol](https://wiki.starbasegame.com/index.php/YOLOL) language.

## Installation

1. Invite the bot to your server by using [this invite link](https://discordapp.com/api/oauth2/authorize?client_id=700054559170756719&permissions=18496&scope=bot).
2. Use `>help` to see a list of all available commands.
3. If you wish to participate in competitions invoke `>subscribe` in a channel. This channel will receive competitions notifications. Join the [Cylon](https://discord.gg/QDam5EV) server for competition discussion!

## Contributing

Yolol-Ladder is [licensed](License.md) under the permission BSD 3-Clause License. PRs to the project are very welcome! If you want to discuss an idea for a PR before implementing it contact Martin#2468 (aka Yolathothep) in [Cylon](https://discord.gg/QDam5EV).

### Overview

 - `Attributes` contains attributes used to decorate other code.
 - `Extensions` contains extension methods.
 - `Modules` contains collections of related Discord commands. New modules must inherit from `ModuleBase`. New commands must be a `public async` method decorated with `Command` and `Summary` attributes.
 - `Serialization/Json` contains an extension to JSON.Net required to correctly serialize Yolol values to/from JSON
 - `Services` contains services which other things (usually modules) can use.
 - `Configuration.cs` defines the command line args passed into the bot.
 - `DiscordBot.cs` connects to Discord and dispatches messages to the command handler.
 - `Program.cs` is the entry point of the program. This sets up the DI container with all the services and then starts the bot.