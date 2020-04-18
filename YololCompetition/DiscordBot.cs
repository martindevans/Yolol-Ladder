using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace YololCompetition
{
    public class DiscordBot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly Configuration _config;
        private readonly IServiceProvider _services;

        public DiscordBot(DiscordSocketClient client, CommandService commands, Configuration config, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _config = config;
            _services = services;
        }

        public async Task Start()
        {
            var tcs = new TaskCompletionSource<bool>();
            _client.Ready += () => {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleMessage;
            _commands.CommandExecuted += CommandExecuted;

            // Log the bot in
            await _client.LogoutAsync();
            await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable(_config.TokenEnvVar));
            await _client.StartAsync();

            // Wait until client is `Ready`
            await tcs.Task;
        }

        private async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess || !result.Error.HasValue || result.Error != CommandError.Exception)
                return;

            await context.Channel.SendMessageAsync("Command Exception! " + result.ErrorReason);
        }

        private async Task HandleMessage(SocketMessage msg)
        {
            // Don't process the command if it was a System Message
            if (!(msg is SocketUserMessage message))
                return;

            // Ignore messages from self
            if (message.Author.Id == _client.CurrentUser.Id)
                return;

            // Check if the message starts with the command prefix character
            var prefixPos = 0;
            var hasPrefix = message.HasCharPrefix(_config.Prefix, ref prefixPos);

            // Skip non-prefixed messages
            if (!hasPrefix)
                return;

            // Execute the command
            var context = new SocketCommandContext(_client, message);
            try
            {
                await _commands.ExecuteAsync(context, prefixPos, _services);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
