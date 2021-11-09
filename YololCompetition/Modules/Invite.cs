using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Services.Subscription;

namespace YololCompetition.Modules
{
    [UsedImplicitly]
    public class Invite
        : ModuleBase
    {
        private readonly ISubscription _subscriptions;
        private readonly Configuration _config;

        public Invite(ISubscription subscriptions, Configuration config)
        {
            _subscriptions = subscriptions;
            _config = config;
        }

        [Command("invite"), Summary("Invite this bot to another server")]
        public async Task GetInvite()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithTitle("Invite Yolol-Ladder")
                .WithDescription($"Once Yolol-Ladder joins your server a user with the `ManageChannels` permission should call `{_config.Prefix}subscribe` in a channel to begin receiving competition alerts in that channel")
                .WithUrl("https://discordapp.com/api/oauth2/authorize?client_id=700054559170756719&permissions=18496&scope=bot")
                .WithFooter("A Cylon Project")
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
    }
}
