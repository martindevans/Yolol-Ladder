using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using YololCompetition.Services.Subscription;

namespace YololCompetition.Modules
{
    public class Invite
        : ModuleBase
    {
        private readonly ISubscription _subscriptions;
        private readonly Configuration _config;
        private readonly DiscordSocketClient _client;

        public Invite(ISubscription subscriptions, Configuration config, DiscordSocketClient client)
        {
            _subscriptions = subscriptions;
            _config = config;
            _client = client;
        }

        [Command("invite"), Summary("Invite this bot to another server")]
        public async Task GetInvite()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithTitle("Invite Yolol-Ladder")
                .WithDescription($"Once Yolol-Ladder joins your server a user with the `ManageChannels` should call `{_config.Prefix}subscribe` in a channel to begin receiving competition alerts in that channel")
                .WithUrl("https://discordapp.com/api/oauth2/authorize?client_id=700054559170756719&permissions=18496&scope=bot")
                .Build()
            );
        }

        [Command("subscribe"), RequireUserPermission(ChannelPermission.ManageChannels), Summary("Begin receiving competition notifications in this channel")]
        public async Task Subscribe()
        {
            await _subscriptions.Subscribe(Context.Channel.Id, Context.Guild.Id);
            await ReplyAsync("Subscribed this channel to Yolol-Ladder. Notifications about competitions will be posted here");
        }

        [Command("unsubscribe"), RequireUserPermission(ChannelPermission.ManageChannels), Summary("Stop receiving competition notifications in this channel")]
        public async Task Unsubscribe()
        {
            await _subscriptions.Unsubscribe(Context.Channel.Id, Context.Guild.Id);
            await ReplyAsync("Subscribed this channel to Yolol-Ladder. Notifications about competitions will be posted here");
        }

        [Command("dump-subscriptions"), RequireOwner, Summary("Print out all subscriptions")]
        public async Task DumpSubs()
        {
            var subs = _subscriptions.GetSubscriptions();

            var output = new StringBuilder();
            await foreach (var sub in subs)
            {
                var guild = _client.GetGuild(sub.Guild);
                var channel = guild.GetTextChannel(sub.Channel);

                var next = $" - {guild?.Name ?? sub.Guild.ToString()} @ {channel?.Name ?? sub.Channel.ToString()}";

                if (output.Length + next.Length > 1000)
                {
                    await ReplyAsync(output.ToString());
                    output.Clear();
                }

                output.AppendLine(next);
            }

            if (output.Length > 0)
                await ReplyAsync(output.ToString());
        }

        [Command("dump-guilds"), RequireOwner, Summary("Print out all guilds this bot is in")]
        public async Task DumpGuilds()
        {
            var output = new StringBuilder();
            foreach (var guild in _client.Guilds)
            {
                var next = $" - {guild?.Name}";

                if (output.Length + next.Length > 1000)
                {
                    await ReplyAsync(output.ToString());
                    output.Clear();
                }

                output.AppendLine(next);
            }

            if (output.Length > 0)
                await ReplyAsync(output.ToString());
        }
    }
}
