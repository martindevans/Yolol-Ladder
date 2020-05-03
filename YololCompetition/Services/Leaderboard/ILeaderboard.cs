using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Leaderboard
{
    public readonly struct RankInfo
    {
        public ulong Id { get; }
        public uint Rank { get; }
        public uint Score { get; }

        public RankInfo(ulong id, uint rank, uint score)
        {
            Id = id;
            Rank = rank;
            Score = score;
        }
    }

    public interface ILeaderboard
    {
        IAsyncEnumerable<RankInfo> GetTopRank(int count);

        Task AddScore(ulong userId, uint score);

        Task SubtractScore(ulong userId, uint score);

        Task<RankInfo?> GetRank(ulong userId);
    }
}
