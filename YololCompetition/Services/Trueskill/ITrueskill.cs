using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Trueskill
{
    public interface ITrueskill
    {
        /// <summary>
        /// Get the trueskill rating for a specific user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public Task<TrueskillRating?> GetRating(ulong userId);

        /// <summary>
        /// Get the top ranked users
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IAsyncEnumerable<TrueskillRating> GetTopRanks(int count);

        /// <summary>
        /// Set the trueskill rating for a specific user. Reset their grace to zero
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="mean"></param>
        /// <param name="stdDev"></param>
        public Task SetRating(ulong userId, double mean, double stdDev);

        /// <summary>
        /// Increment the grace counter for all players and decay rankings
        /// </summary>
        /// <param name="gracePeriod">Rankings with a grace value >= gracePeriod will decay</param>
        /// <param name="amount"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public Task Decay(int gracePeriod, double amount = 1.1f, double threshold = 3.5);

        /// <summary>
        /// Clear all ratings
        /// </summary>
        /// <returns></returns>
        public Task Clear();
    }

    public readonly struct TrueskillRating
    {
        public ulong UserId { get; }
        public uint Rank { get; }

        public double Mean { get; }
        public double StdDev { get; }
        public double ConservativeEstimate => Mean - 3 * StdDev;

        public TrueskillRating(ulong userId, uint rank, double mean, double stdDev)
        {
            UserId = userId;
            Mean = mean;
            StdDev = stdDev;
            Rank = rank;
        }
    }
}
