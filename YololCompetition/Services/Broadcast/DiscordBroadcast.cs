﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using YololCompetition.Services.Subscription;

namespace YololCompetition.Services.Broadcast
{
    public class DiscordBroadcast
        : IBroadcast
    {
        private readonly ISubscription _subscriptions;
        private readonly IDiscordClient _client;

        public DiscordBroadcast(ISubscription subscriptions, DiscordSocketClient client)
        {
            _subscriptions = subscriptions;
            _client = client;
        }

        public async IAsyncEnumerable<IUserMessage> Broadcast(Embed embed)
        {
            var subs = await _subscriptions.GetSubscriptions().ToArrayAsync();
            foreach (var subscription in subs)
            {
                var guild = await _client.GetGuildAsync(subscription.Guild);
                if (guild == null)
                    continue;

                var channel = await guild.GetTextChannelAsync(subscription.Channel);
                if (channel == null)
                    continue;

                yield return await channel.SendMessageAsync(embed: embed);
                await Task.Delay(100);
            }
        }

        public async Task Broadcast(string message)
        {
            var subs = await _subscriptions.GetSubscriptions().ToArrayAsync();
            foreach (var subscription in subs)
            {
                var guild = await _client.GetGuildAsync(subscription.Guild);
                if (guild == null)
                    continue;

                var channel = await guild.GetTextChannelAsync(subscription.Channel);
                if (channel == null)
                    continue;

                await channel.SendMessageAsync(message);
                await Task.Delay(100);
            }
        }
    }
}
