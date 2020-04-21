using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Leaderboard
{
    public struct RankInfo
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
        IAsyncEnumerable<RankInfo> GetUserNearRanks(ulong userId, byte above, byte below);

        IAsyncEnumerable<RankInfo> GetTopRank(byte count);

        Task AddScore(ulong userId, uint score);

        Task SubtractScore(ulong userId, uint score);
    }
}
