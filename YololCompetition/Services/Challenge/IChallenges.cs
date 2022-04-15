using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YololCompetition.Services.Challenge
{
    public interface IChallenges
    {
        Task Create(Challenge challenge);

        Task Update(Challenge challenge);

        Task<long> GetPendingCount();

        Task<Challenge?> GetCurrentChallenge();

        Task<Challenge?> StartNext();

        Task<int> SetToPending(ulong challengeId);

        Task EndCurrentChallenge(bool terminate = false);

        Task ChangeChallengeDifficulty(Challenge challenge, ChallengeDifficulty difficulty);

        IAsyncEnumerable<Challenge> GetChallenges(ChallengeDifficulty? difficultyFilter = null, ulong? id = null, string? name = null, bool includeUnstarted = false);

        Task<int> Delete(ulong challengeId);

        IAsyncEnumerable<Challenge> GetChallengesByEndTime(ulong EndUnixTime);
    }

    public enum ChallengeStatus
    {
        None = 0,

        Pending = 1,
        Running = 2,
        Complete = 3,

        TestMode = 4,

        Terminated = 5
    }

    public static class IChallengesExtensions
    {
        public static IAsyncEnumerable<Challenge> FuzzyFindChallenge(this IChallenges challenges, string search, bool includeUnstarted = false)
        {
            // Try parsing the string as a challenge ID
            var uid = BalderHash.BalderHash32.Parse(search);
            if (uid.HasValue)
                return challenges.GetChallenges(id: uid.Value.Value, includeUnstarted: includeUnstarted).Take(1);

            // Try searching for a challenge that matches the name
            return challenges.GetChallenges(name: $"%{search}%");
        }
    }
}
