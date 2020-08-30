﻿using System;
using System.Threading;
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

        private const int MaxWaitTimeMs = 2500;
        private readonly SemaphoreSlim _commandConcurrencyLimit = new SemaphoreSlim(100);

        public DiscordBot(DiscordSocketClient client, CommandService commands, Configuration config, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
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

            // Log the bot in
            await _client.LogoutAsync();
            await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable(_config.TokenEnvVar));
            await _client.StartAsync();

            // Wait until connected
            while (_client.ConnectionState == ConnectionState.Connecting)
                await Task.Delay(1);

            // Wait until client is `Ready`
            await ready.Task;
        }

        private async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
                return;

            if (!result.Error.HasValue)
            {
                await context.Channel.SendMessageAsync("Command failed (no error)");
                return;
            }

            if (result.Error == CommandError.Exception)
            {
                await context.Channel.SendMessageAsync("Command Exception! " + result.ErrorReason);
                return;
            }

            if (result.Error == CommandError.UnmetPrecondition)
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
                return;
            }

            if (result.Error == CommandError.Exception)
            {
                await context.Channel.SendMessageAsync($"Command Failed! {result.ErrorReason}");
                return;
            }
        }

        private async Task HandleMessage(SocketMessage msg)
        {
            try
            {
                if (_commandConcurrencyLimit.CurrentCount == 0)
                    await msg.Channel.SendMessageAsync("Bot is busy - waiting in queue.");

                // Wait until this command is allowed to be serviced (limit total command concurrency)
                if (!await _commandConcurrencyLimit.WaitAsync(MaxWaitTimeMs))
                    await msg.Channel.SendMessageAsync("Bot is too busy. Please try again later.");

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
            finally
            {
                _commandConcurrencyLimit.Release();
            }
        }
    }
}
