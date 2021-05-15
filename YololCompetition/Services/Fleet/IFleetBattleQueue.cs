using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Fleet
{
    public interface IFleetBattleQueue
    {
        Task Enqueue(Fleet fleet);

        Task<IReadOnlyList<Battle>> Queue(uint limit = uint.MaxValue);

        Task Remove(Battle battle);

        Task<Battle?> GetNextBattle();
    }
}
