using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using ExecuteResult = Discord.Commands.ExecuteResult;
using IResult = Discord.Commands.IResult;

namespace YololCompetition
{
    public class DiscordBot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly Configuration _config;
        private readonly IServiceProvider _services;

        private const int MaxWaitTimeMs = 3500;
        private readonly SemaphoreSlim _commandConcurrencyLimit = new(50);

        public DiscordBot(DiscordSocketClient client, CommandService commands, InteractionService interactions, Configuration config, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _interactions = interactions;
            _config = config;
            _services = services;
        }

        public async Task Start()
        {
            var ready = new TaskCompletionSource<bool>();
            _client.Ready += () => {
                ready.SetResult(true);
                return Task.CompletedTask;
            };

            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleMessage;
            _commands.CommandExecuted += CommandExecuted;
            _client.SlashCommandExecuted += a => _interactions.ExecuteCommandAsync(new InteractionContext(_client, a, a.Channel), _services);

            // Log the bot in
            await _client.LogoutAsync();
            var token = Environment.GetEnvironmentVariable(_config.TokenEnvVar);
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Wait until connected
            while (_client.ConnectionState == ConnectionState.Connecting)
                await Task.Delay(1);

            // Wait until client is `Ready`
            Console.WriteLine("Waiting for ready event...");
            await ready.Task;

            // Set nickname in all guilds
            _client.JoinedGuild += async sg => {
                await TrySetNickname(sg.CurrentUser);
            };
            foreach (var clientGuild in _client.Guilds)
                await TrySetNickname(clientGuild.CurrentUser);
        }

        private static async Task TrySetNickname(SocketGuildUser sgu)
        {
            try
            {
                // Check if the bot has nickname permission before trying to set nickname
                if (!sgu.GuildPermissions.Has(GuildPermission.ChangeNickname))
                    return;
                await sgu.ModifyAsync(async gup => { gup.Nickname = "Referee"; });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to set nickname in `{sgu.Guild.Name}`: {e}");
            }
        }

        private static async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
                return;

            if (!result.Error.HasValue)
            {
                await context.Channel.SendMessageAsync("Command failed (no error)");
                return;
            }

            switch (result.Error)
            {
                case CommandError.UnmetPrecondition:
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                    break;

                case CommandError.Exception:
                    if (result is ExecuteResult exr)
                        Console.WriteLine(exr.Exception);
                    await context.Channel.SendMessageAsync($"Command Failed! {result.ErrorReason}");
                    break;
            }
        }

        private async Task HandleMessage(SocketMessage msg)
        {
            try
            {
                // Don't process the command if it was a System Message
                if (msg is not SocketUserMessage message)
                    return;

                // Ignore messages from self
                if (message.Author.Id == _client.CurrentUser.Id)
                    return;

                // Check if the message starts with the command prefix character
                var prefixPos = 0;
                var hasPrefix = message.HasCharPrefix(_config.Prefix, ref prefixPos);

                // Skip non-prefixed messages
                if (!hasPrefix)
                {
                    HandleNonCommand(msg);
                    return;
                }

                if (_commandConcurrencyLimit.CurrentCount == 0)
                    await msg.Channel.SendMessageAsync("Bot is busy - waiting in queue.");

                // Wait until this command is allowed to be serviced (limit total command concurrency)
                if (!await _commandConcurrencyLimit.WaitAsync(MaxWaitTimeMs))
                {
                    await msg.Channel.SendMessageAsync("Bot is too busy. Please try again later.");
                    return;
                }

                // Execute the command
                var context = new SocketCommandContext(_client, message);
                try
                {
                    var result = await _commands.ExecuteAsync(context, prefixPos, _services);
                    PostCommandResult(result);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            finally
            {
                _commandConcurrencyLimit.Release();
            }
        }

        private static void HandleNonCommand(SocketMessage _)
        {
        }

        private static void PostCommandResult(IResult _)
        {
            //var s = result.IsSuccess;
            //var e = result.Error;
            //var r = result.ErrorReason;
            //Console.WriteLine(s);
            //Console.WriteLine(e);
            //Console.WriteLine(r);
        }
    }
}
