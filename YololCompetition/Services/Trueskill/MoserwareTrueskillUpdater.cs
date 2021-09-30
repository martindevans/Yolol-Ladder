using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moserware.Skills;
using YololCompetition.Services.Solutions;

namespace YololCompetition.Services.Trueskill
{
    public class MoserwareTrueskillUpdater
        : ITrueskillUpdater
    {
        private readonly ISolutions _solutions;
        private readonly ITrueskill _ratings;
        private readonly GameInfo _gameInfo;

        public MoserwareTrueskillUpdater(ISolutions solutions, ITrueskill ratings, GameInfo gameInfo)
        {
            _solutions = solutions;
            _ratings = ratings;
            _gameInfo = gameInfo;
        }

        private async Task<UserTrueskillRating> GetOrAddRating(ulong userId)
        {
            var rating = await _ratings.GetRating(userId);
            if (rating.HasValue)
                return rating.Value;

            rating = new UserTrueskillRating(userId, uint.MaxValue, new TrueskillRating(_gameInfo.DefaultRating.Mean, _gameInfo.DefaultRating.StandardDeviation));
            await _ratings.SetRating(userId, rating.Value.Rating.Mean, rating.Value.Rating.StdDev);
            return rating.Value;
        }

        private static Rating Convert(UserTrueskillRating rating)
        {
            return new Rating(rating.Rating.Mean, rating.Rating.StdDev);
        }

        public async Task ApplyChallengeResults(ulong challengeId)
        {
            // Convert all solutions for the current challenge into a set of relevant data
            var teamRanks = await _solutions.GetSolutions(challengeId, uint.MaxValue)
                .AsAsyncEnumerable()
                .OrderBy(a => a.Rank)
                .SelectAwait(async a => new {
                    player = a.Solution.UserId,
                    rank = a.Rank,
                    rating = Convert(await GetOrAddRating(a.Solution.UserId)),
                })
                .Select(a => new {
                    team = new Dictionary<ulong, Rating> {{a.player, a.rating}},
                    a.rank
                }).ToArrayAsync();

            // Trueskill can't be applied if there were less than 2 entrants
            if (teamRanks.Length > 1)
            {
                // Extract the data out of that into separate collections
                var teams = teamRanks.Select(a => a.team).ToArray();
                var ranks = teamRanks.Select(a => (int)a.rank).ToArray();

                // Calculate trueskill ratings
                var results = TrueSkillCalculator.CalculateNewRatings(
                    GameInfo.DefaultGameInfo,
                    teams,
                    ranks
                );

                // Update database with new ratings
                foreach (var (key, rating) in results)
                    await _ratings.SetRating(key, rating.Mean, rating.StandardDeviation);
            }

            // Decay rank of all players who did not participate in this challenge
            await _ratings.Decay(4);
        }
    }
}
