using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Fleet
{
    public interface IFleetRankings
    {
        Task ResetRankDeviation(Fleet fleet);

        Task<IReadOnlyList<FleetTrueskillRating>> GetTopTanks(uint limit);

        Task Update(ulong winner, ulong loser, bool draw);

        Task<int> DeleteRank(ulong id);
    }
}
