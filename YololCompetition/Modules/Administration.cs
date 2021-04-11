using System;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using YololCompetition.Services.Subscription;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class Administration
        : ModuleBase
    {
        private readonly ISubscription _subscriptions;
        private readonly DiscordSocketClient _client;

        public Administration(ISubscription subscriptions, DiscordSocketClient client)
        {
            _subscriptions = subscriptions;
            _client = client;
        }

        [Command("kill"), RequireOwner, Summary("Immediately kill the bot")]
        public async Task Kill(int exitCode = 1)
        {
            await ReplyAsync("x_x");
            Environment.Exit(exitCode);
        }

        [Command("simd"), RequireOwner]
        public async Task Simd()
        {
            var embed = new EmbedBuilder().WithTitle("SIMD Support").WithDescription(
                $" - AVX:  {Avx.IsSupported}\n" +
                $" - AVX2: {Avx2.IsSupported}\n" + 
                $" - BMI1: {Bmi1.IsSupported}\n" + 
                $" - BMI2: {Bmi2.IsSupported}\n" + 
                $" - FMA:  {Fma.IsSupported}\n" + 
                $" - LZCNT:{Lzcnt.IsSupported}\n" + 
                $" - PCLMULQDQ:{Pclmulqdq.IsSupported}\n" + 
                $" - POPCNT:{Popcnt.IsSupported}\n" + 
                $" - POPCNT:{Popcnt.IsSupported}\n" + 
                $" - SSE:{Sse.IsSupported}\n" + 
                $" - SSE2:{Sse2.IsSupported}\n" + 
                $" - SSE3:{Sse3.IsSupported}\n" + 
                $" - SSSE3:{Ssse3.IsSupported}\n" + 
                $" - SSE41:{Sse41.IsSupported}\n" + 
                $" - SSE42:{Sse42.IsSupported}\n"
            ).Build();

            await ReplyAsync(embed: embed);
        }

        [Command("dump-subscriptions"), RequireOwner, Summary("Print out all subscriptions")]
        public async Task DumpSubs()
        {
            var subs = _subscriptions.GetSubscriptions();

            var output = new StringBuilder();
            output.AppendLine("Bot Subscriptions:");
            await foreach (var sub in subs)
            {
                var guild = _client.GetGuild(sub.Guild);
                var channel = guild.GetTextChannel(sub.Channel);

                var next = $" - Guild:`{guild.Name ?? sub.Guild.ToString()}`, Channel:`{channel?.Name ?? sub.Channel.ToString()}`";

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
            output.AppendLine("Bot Guilds:");
            foreach (var guild in _client.Guilds)
            {
                await guild.DownloadUsersAsync();
                var next = $" - Guild:`{guild.Name}`, Owner:`{guild.Owner.Username}#{guild.Owner.Discriminator}`";

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
