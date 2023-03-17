using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace YololCompetition.Services.Interactive
{
    public class InteractiveService
        : IInteractive
    {
        private readonly DiscordSocketClient _client;

        private readonly HashSet<MessageWaiter> _waiters = new();

        public InteractiveService(DiscordSocketClient client)
        {
            _client = client;
        }

        public void Start()
        {
            _client.MessageReceived += HandleMessage;
        }

        private async Task HandleMessage(SocketMessage arg)
        {
            lock (_waiters)
            {
                if (_waiters.Count == 0)
                    return;

                var remove = new List<MessageWaiter>();
                foreach (var waiter in _waiters)
                {
                    if (waiter.TryComplete(arg))
                        remove.Add(waiter);
                }

                _waiters.ExceptWith(remove);
            }
        }

        public async Task<IMessage?> NextMessageAsync(IUser source, IChannel channel, TimeSpan timeout)
        {
            // Setup the waiter to receive messages into a completion source
            var tcs = new TaskCompletionSource<IMessage>();
            var waiter = new MessageWaiter(source, channel, tcs);
            lock (_waiters)
                _waiters.Add(waiter);

            // Wait for the timeout or a result
            await Task.WhenAny(
                Task.Delay(timeout),
                tcs.Task
            );

            // Make sure to clean up
            lock (_waiters)
                _waiters.Remove(waiter);

            if (tcs.Task.IsCompleted)
                return await tcs.Task;
            return null;
        }

        private class MessageWaiter
        {
            private readonly IUser _source;
            private readonly IChannel _channel;
            private readonly TaskCompletionSource<IMessage> _tcs;

            public MessageWaiter(IUser source, IChannel channel, TaskCompletionSource<IMessage> tcs)
            {
                _source = source;
                _channel = channel;
                _tcs = tcs;
            }

            public bool TryComplete(IMessage socketMessage)
            {
                if (socketMessage.Author.Id != _source.Id)
                    return false;
                if (socketMessage.Channel.Id != _channel.Id)
                    return false;

                _tcs.SetResult(socketMessage);
                return true;
            }
        }
    }
}
