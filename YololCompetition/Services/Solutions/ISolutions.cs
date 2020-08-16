using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Solutions
{
    public interface ISolutions
    {
        Task SetSolution(Solution solution);

        Task<Solution?> GetSolution(ulong userId, ulong challengeId);

        IAsyncEnumerable<RankedSolution> GetSolutions(ulong challengeId, uint limit, uint minScore = uint.MinValue, uint maxScore = uint.MaxValue);

        Task<RankedSolution?> GetRank(ulong challengeId, ulong userId);

        async IAsyncEnumerable<RankedSolution> GetTopRank(ulong challengeId)
        {
            await foreach (var solution in GetSolutions(challengeId, 100))
            {
                if (solution.Rank == 1)
                    yield return solution;
                else
                    break;
            }
        }

        Task<int> DeleteSolution(ulong challengeId, ulong userId);
    }

    public readonly struct RankedSolution
    {
        public Solution Solution { get; }
        public uint Rank { get; }

        public RankedSolution(Solution solution, uint rank)
        {
            Solution = solution;
            Rank = rank;
        }
    }

    public readonly struct Solution
    {
        public ulong ChallengeId { get; }
        public ulong UserId { get; }
        public uint Score { get; }
        public string Yolol { get; }

        public Solution(ulong challenge, ulong user, uint score, string yolol)
        {
            ChallengeId = challenge;
            UserId = user;
            Score = score;
            Yolol = yolol;
        }
    }
}
