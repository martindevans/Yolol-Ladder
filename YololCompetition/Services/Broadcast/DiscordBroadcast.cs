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

        public async Task Broadcast(Embed embed)
        {
            await foreach (var subscription in _subscriptions.GetSubscriptions())
            {
                var guild = await _client.GetGuildAsync(subscription.Guild);
                if (guild == null)
                    continue;

                var channel = await guild.GetTextChannelAsync(subscription.Channel);
                if (channel == null)
                    continue;

                await channel.SendMessageAsync(embed: embed);
                await Task.Delay(100);
            }
        }

        public async Task Broadcast(string message)
        {
            await foreach (var subscription in _subscriptions.GetSubscriptions())
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
