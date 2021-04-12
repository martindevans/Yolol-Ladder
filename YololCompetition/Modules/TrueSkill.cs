using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JetBrains.Annotations;
using Moserware.Skills;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Modules
{
    [RequireOwner]
    [UsedImplicitly]
    public class TrueSkill
        : ModuleBase
    {
        private readonly IChallenges _challenges;
        private readonly DiscordSocketClient _client;
        private readonly ITrueskill _skill;
        private readonly ITrueskillUpdater _updater;

        public TrueSkill(IChallenges challenges, DiscordSocketClient client, ITrueskill skill, ITrueskillUpdater updater)
        {
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

        private async ValueTask<string> FormatRankInfo(TrueskillRating rating)
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
    }
}
