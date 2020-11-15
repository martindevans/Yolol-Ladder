using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Moserware.Skills;
using YololCompetition.Extensions;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class TrueSkill
        : ModuleBase
    {
        private readonly ISolutions _solutions;
        private readonly IChallenges _challenges;
        private readonly DiscordSocketClient _client;
        private readonly ITrueskill _skill;
        private readonly ITrueskillUpdater _updater;

        public TrueSkill(ISolutions solutions, IChallenges challenges, DiscordSocketClient client, ITrueskill skill, ITrueskillUpdater updater)
        {
            _solutions = solutions;
            _challenges = challenges;
            _client = client;
            _skill = skill;
            _updater = updater;
        }

        [Command("seed-trueskill"), RequireOwner, Summary("Reseed trueskill table")]
        public async Task Seed()
        {
            await _skill.Clear();

            await foreach (var challenge in _challenges.GetChallenges().Where(a => a.EndTime.HasValue).OrderBy(a => a.EndTime))
            {
                await _updater.ApplyChallengeResults(challenge.Id);

                // Display updated ranks
                StringBuilder s = new StringBuilder($"{challenge.Name}\n");
                s.Append(string.Join("\n",
                    await _skill.GetTopRanks(int.MaxValue)
                          .OrderByDescending(a => a.ConservativeEstimate)
                          .SelectAwait(FormatRankInfo)
                          .ToArrayAsync()
                ));

                // Display ranks
                await ReplyAsync(s.ToString());

                await Task.Delay(2000);
            }

            await ReplyAsync("Done.");
        }

        async ValueTask<string> FormatRankInfo(TrueskillRating rating)
        {
            return await FormatRankInfo(new KeyValuePair<ulong, Rating>(rating.UserId, new Rating(rating.Mean, rating.StdDev)));
        }

        private async ValueTask<string> FormatRankInfo(KeyValuePair<ulong, Rating> item)
        {
            var id = item.Key;
            var rating = item.Value;

            var user = (IUser)_client.GetUser(id) ?? await _client.Rest.GetUserAsync(id);
            var name = user?.Username ?? id.ToString();
            return $"**{name}**: R:{rating.ConservativeRating:0.00}, M:{rating.Mean:0.00}, D:{rating.StandardDeviation:0.00}";
        }

        [Command("calculate-trueskill"), RequireOwner, Summary("Calculate the trueskill rank for all players")]
        public async Task Calculate()
        {
            var playerRatings = new Dictionary<ulong, Rating>();

            await foreach (var challenge in _challenges.GetChallenges().Where(a => a.EndTime.HasValue).OrderBy(a => a.EndTime))
            {
                // Convert all solutions for the current challenge into a set of teams
                var teamRanks = await 
                    (from solution in _solutions.GetSolutions(challenge.Id, uint.MaxValue)
                    orderby solution.Rank
                    let player = solution.Solution.UserId
                    let rating = playerRatings.GetOrAdd(player, GameInfo.DefaultGameInfo.DefaultRating)
                    let team = new Dictionary<ulong, Rating> {{player, rating}}
                    select (team, solution.Rank)).ToArrayAsync();

                var users = new HashSet<ulong>(teamRanks.SelectMany(a => a.team.Keys));
                var teams = teamRanks.Select(a => a.team).ToArray();
                var ranks = teamRanks.Select(a => (int)a.Rank).ToArray();

                // Calculate trueskill ratings
                var results = TrueSkillCalculator.CalculateNewRatings(
                    GameInfo.DefaultGameInfo,
                    teams,
                    ranks
                );

                // Copy into ranking "database"
                foreach (var result in results)
                    playerRatings[result.Key] = result.Value;

                // Display updated ranks
                StringBuilder s = new StringBuilder($"{challenge.Name}\n");
                s.Append(string.Join("\n",
                    await playerRatings
                          .OrderByDescending(a => a.Value.ConservativeRating)
                          .ToAsyncEnumerable()
                          .SelectAwait(FormatRankInfo)
                          .ToArrayAsync()
                ));

                // Display ranks
                await ReplyAsync(s.ToString());

                // Decay ranks of non participating players who have a small stddev
                var decay = playerRatings
                            //.Where(u => !users.Contains(u.Key))
                            .Where(u => u.Value.StandardDeviation < 4)
                            .ToArray();
                foreach (var playerRating in decay)
                    playerRatings[playerRating.Key] = new Rating(playerRating.Value.Mean, playerRating.Value.StandardDeviation * 1.1f);

                await Task.Delay(1000);
            }

            await ReplyAsync("Done.");
        }
    }
}
