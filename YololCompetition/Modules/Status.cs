using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;

namespace YololCompetition.Modules
{
    [Hidden]
    public class Status
        : ModuleBase
    {
        private readonly DiscordSocketClient _client;

        public Status(DiscordSocketClient client)
        {
            _client = client;
        }

        [Command("memory"), RequireOwner, Summary("Print current memory stats")]
        public async Task MemoryUsage()
        {
            var embed = new EmbedBuilder()
                        .AddField("Working Set", Environment.WorkingSet.Bytes().Humanize("#.##"), true)
                        .AddField("GC Total Memory", GC.GetTotalMemory(false).Bytes().Humanize("#.##"), true)
                        .Build();

            await ReplyAsync(embed: embed);
        }

        [Command("hostinfo"), RequireOwner, Summary("Print HostInfo")]
        public async Task HostInfo()
        {
            var embed = new EmbedBuilder()
                        .AddField("Machine", Environment.MachineName)
                        .AddField("User", Environment.UserName)
                        .AddField("OS", Environment.OSVersion)
                        .AddField("CPUs", Environment.ProcessorCount)
                        .Build();

            await ReplyAsync("", false, embed);
        }

        [Command("ping"), Summary("Respond with `Pong`"), Alias("test")]
        public async Task Ping()
        {
            await ReplyAsync("pong");
        }

        [Command("latency"), Summary("Show current latency between Bot and Discord")]
        public async Task Latency()
        {
            var latency = TimeSpan.FromMilliseconds(_client.Latency);
            await ReplyAsync($"Latency: {latency.TotalMilliseconds}");
        }

        [Command("shard"), Summary("Print Shard ID")]
        public async Task Shard()
        {
            await ReplyAsync($"Shard ID: {_client.ShardId}");
        }

        [Command("kill"), RequireOwner, Summary("Immediately kill the bot")]
        public async Task Kill(int exitCode = 1)
        {
            await Task.CompletedTask;
            Environment.Exit(exitCode);
        }
    }
}
