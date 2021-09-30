using System.Threading.Tasks;

namespace YololCompetition.Services.Trueskill
{
    public interface ITrueskillUpdater
    {
        Task ApplyChallengeResults(ulong challenge);
    }
}
