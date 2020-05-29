using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Challenge
{
    public interface IChallenges
    {
        Task Create(Challenge challenge);

        Task<long> GetPendingCount();

        Task<Challenge?> GetCurrentChallenge();

        Task<Challenge?> StartNext();

        Task EndCurrentChallenge();

        Task ChangeChallengeDifficulty(Challenge challenge, ChallengeDifficulty difficulty);

        IAsyncEnumerable<Challenge> GetChallenges(ChallengeDifficulty? difficultyFilter = null, ulong? id = null, string? name = null, bool includeUnstarted = false);
    }

    public static class IChallengesExtensions
    {
        public static IAsyncEnumerable<Challenge> FuzzyFindChallenge(this IChallenges challenges, string search)
        {
            // Try parsing the string as a challenge ID
            var uid = BalderHash.BalderHash32.Parse(search);
            if (uid.HasValue)
                return challenges.GetChallenges(id: uid.Value.Value).Take(1);

            // Try searching for a challenge that matches the name
            return challenges.GetChallenges(name: $"%{search}%");
        }
    }
}
