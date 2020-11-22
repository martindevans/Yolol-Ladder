using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Leaderboard;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Modules
{
    public class Leaderboard
        : ModuleBase
    {
        private readonly ILeaderboard _leaderboard;
        private readonly DiscordSocketClient _client;
        private readonly IChallenges _challenges;
        private readonly ISolutions _solutions;
        private readonly ITrueskill _skills;

        public Leaderboard(ILeaderboard leaderboard, DiscordSocketClient client, IChallenges challenges, ISolutions solutions, ITrueskill skills)
        {
            _leaderboard = leaderboard;
            _client = client;
            _challenges = challenges;
            _solutions = solutions;
            _skills = skills;
        }

        public enum LeaderboardType
        {
            Global,
            Current,
            Trueskill,
        }

        [Command("leaderboard"), Summary("Display the top Yolol programmers")]
        public async Task ShowLeaderboard(LeaderboardType type = LeaderboardType.Global)
        {
            if (type == LeaderboardType.Global)
            {
                var top = _leaderboard.GetTopRank(15);
                await ReplyAsync(embed: await FormatLeaderboard(top));
            }
            else if (type == LeaderboardType.Current)
            {
                var challenge = await _challenges.GetCurrentChallenge();
                if (challenge == null)
                {
                    await ReplyAsync("There is no currently running challenge");
                    return;
                }

                await DisplayChallengeLeaderboard(challenge);
            }
            else if (type == LeaderboardType.Trueskill)
            {
                var top = _skills.GetTopRanks(15);
                await ReplyAsync(embed: await FormatLeaderboard(top));
            }
            else
                throw new InvalidOperationException("Unknown leaderboard type");

            
        }

        [Command("leaderboard"), Summary("Display the top Yolol programmers")]
        public async Task ShowLeaderboard(string id)
        {
            var c = await _challenges.FuzzyFindChallenge(id).Take(2).ToArrayAsync();
            if (c.Length > 1)
            {
                await ReplyAsync("Found more than one challenge matching that search string, please be more specific");
            }
            else if (c.Length == 0)
            {
                await ReplyAsync("Could not find a challenge matching that searching string");
            }
            else
            {
                await DisplayChallengeLeaderboard(c[0]);
            }
        }

        private async Task DisplayChallengeLeaderboard(Challenge challenge)
        {
            // Get the top N solutions
            var top5 = _solutions.GetSolutions(challenge.Id, 20).Select(a => new RankInfo(a.Solution.UserId, a.Rank, a.Solution.Score));

            // Get your own rank
            var self = await _solutions.GetRank(challenge.Id, Context.User.Id);
            RankInfo? selfRank = null;
            if (self.HasValue)
                selfRank = new RankInfo(self.Value.Solution.UserId, self.Value.Rank, self.Value.Solution.Score);

            await ReplyAsync(embed: await FormatLeaderboard(top5, selfRank, challenge));

        }

        [RequireOwner]
        [Command("give-points"), Summary("Give some points to a user")]
        public async Task GivePoints(IUser user, uint score)
        {
            await _leaderboard.AddScore(user.Id, score);
        }

        [RequireOwner]
        [Command("deduct-points"), Summary("Remove some points from a user")]
        public async Task SubPoints(IUser user, uint score)
        {
            await _leaderboard.SubtractScore(user.Id, score);
        }

        private async Task<Embed> FormatLeaderboard(IAsyncEnumerable<RankInfo> ranks, RankInfo? extra = null, Challenge? challenge = null)
        {
            async Task<string> FormatRankInfo(RankInfo info)
            {
                var user = (IUser)_client.GetUser(info.Id) ?? await _client.Rest.GetUserAsync(info.Id);
                var name = user?.Username ?? info.Id.ToString();
                return $"{info.Rank}. **{name}**\n\u2003Score:{info.Score}";
            }

            var embed = new EmbedBuilder {
                Title = challenge?.Name ?? "Yolol Leaderboard",
                Color = Color.Green,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            var count = 0;
            var seenExtra = false;
            var builder = new StringBuilder();
            await foreach (var rank in ranks)
            {
                builder.AppendLine(await FormatRankInfo(rank));
                seenExtra |= extra.HasValue && rank.Id == extra.Value.Id;
                count++;
            }

            if (!seenExtra && extra.HasValue)
            {
                if (count > 0)
                    builder.AppendLine("...");
                builder.AppendLine(await FormatRankInfo(extra.Value));
            }

            if (!seenExtra && count == 0)
                builder.AppendLine("Leaderboard is empty!");

            embed.WithDescription(builder.ToString());

            return embed.Build();
        }

        private async Task<Embed> FormatLeaderboard(IAsyncEnumerable<TrueskillRating> ranks)
        {
            async Task<string> FormatRankInfo(TrueskillRating info)
            {
                var skill = 100 * Math.Max(0, info.ConservativeEstimate);
                var user = (IUser)_client.GetUser(info.UserId) ?? await _client.Rest.GetUserAsync(info.UserId);
                var name = user?.Username ?? info.UserId.ToString();
                return $"{info.Rank}. ({skill:0000}) **{name}**";
            }

            var embed = new EmbedBuilder {
                Title = "Yolol Leaderboard",
                Color = Color.Green,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            var count = 0;
            var builder = new StringBuilder();
            await foreach (var rank in ranks)
            {
                builder.AppendLine(await FormatRankInfo(rank));
                count++;
            }

            if (count == 0)
                builder.AppendLine("Leaderboard is empty!");

            embed.WithDescription(builder.ToString());

            return embed.Build();
        }
    }
}
