using System.Threading.Tasks;

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
    }
}
