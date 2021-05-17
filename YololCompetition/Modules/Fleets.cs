﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using YololCompetition.Services.Fleet;

namespace YololCompetition.Modules
{
    [Hidden, UsedImplicitly]
    [Group("fleet")]
    public class Fleets
        : BaseModule
    {
        private readonly DiscordSocketClient _client;
        private readonly IFleetStorage _fleets;
        private readonly IFleetBattleQueue _battles;
        private readonly IFleetRankings _rankings;

        public Fleets(DiscordSocketClient client, IFleetStorage fleets, IFleetBattleQueue battles, IFleetRankings rankings)
        {
            _client = client;
            _fleets = fleets;
            _battles = battles;
            _rankings = rankings;
        }

        [Command("submit"), Summary("Submit a new fleet to the space battle.")]
        public async Task SubmitFleet([Remainder] string name)
        {
            name = name.Replace("\"", "")
                .Replace("\\", "")
                .Replace("//", "")
                .Replace(".", "");

            if (Context.Message.Attachments.Count == 0)
            {
                await ReplyAsync("Must attach the fleet as a zip file!");
                return;
            }

            if (Context.Message.Attachments.Count > 1)
            {
                await ReplyAsync("Too many attachments!");
                return;
            }

            var file = Context.Message.Attachments.Single();
            if (file.Size > 10000)
            {
                await ReplyAsync("Fleet file is too large (> 10KB)! Contact Martin#2468 to request a larger limit.");
                return;
            }

            using var client = new WebClient();
            var bytes = await client.DownloadDataTaskAsync(file.Url);

            // Store this new fleet in the DB
            var fleet = await _fleets.Store(Context.User.Id, name, bytes);

            // Clear rankings for this fleet (if they exist)
            await _rankings.ResetRank(fleet);

            // Enqueue battles for this fleet against all other fleets
            await _battles.Enqueue(fleet);

            await ReplyAsync($"Saved fleet `{name}`. Beginning battle simulation.");
        }

        [Command("queue"), Summary("Check the queue of battles.")]
        public async Task CheckQueue()
        {
            var queue = await _battles.Queue();

            async Task<string> FormatFleet(Fleet? fleet)
            {
                return await (fleet?.FormattedName(_client) ?? Task.FromResult("Unknown"));
            }

            async Task<string> FormatBattle(Battle battle, int index)
            {
                var fa = await FormatFleet(await _fleets.Load(battle.A));
                var fb = await FormatFleet(await _fleets.Load(battle.B));
                return $"{index + 1}. {fa} vs {fb}";
            }

            await DisplayItemList(
                queue,
                () => "No pending battles",
                FormatBattle
            );
        }

        [Command("leaderboard"), Summary("Check the leaderboard of fleets.")]
        public async Task Leaderboard()
        {
            var rankings = await _rankings.GetTopTanks(25);
            await ReplyAsync(embed: await FormatLeaderboard(_client, rankings));
        }

        private static async Task<Embed> FormatLeaderboard(BaseSocketClient client, IEnumerable<FleetTrueskillRating> ranks)
        {
            async Task<string> FormatRankInfo(int count, FleetTrueskillRating info)
            {
                var skill = 100 * Math.Max(0, info.Rating.ConservativeEstimate);
                var user = (IUser)client.GetUser(info.Fleet.OwnerId) ?? await client.Rest.GetUserAsync(info.Fleet.OwnerId);
                var name = user?.Username ?? info.Fleet.OwnerId.ToString();
                return $"{count}. **{info.Fleet.Name}** ({name}) - {skill:0000}";
            }

            var embed = new EmbedBuilder {
                Title = "Yolol Fleet Leaderboard",
                Color = Color.Purple,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            var count = 1;
            var builder = new StringBuilder();
            foreach (var rank in ranks)
            {
                builder.AppendLine(await FormatRankInfo(count, rank));
                count++;
            }

            if (count == 0)
                builder.AppendLine("Leaderboard is empty!");

            embed.WithDescription(builder.ToString());

            return embed.Build();
        }
    }
}