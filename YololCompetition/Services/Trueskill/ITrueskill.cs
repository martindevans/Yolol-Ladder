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
        public Task<UserTrueskillRating?> GetRating(ulong userId);

        /// <summary>
        /// Get the top ranked users
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IAsyncEnumerable<UserTrueskillRating> GetTopRanks(int count);

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

    public readonly struct UserTrueskillRating
    {
        public ulong UserId { get; }
        public uint Rank { get; }

        public TrueskillRating Rating { get; }

        public UserTrueskillRating(ulong userId, uint rank, TrueskillRating rating)
        {
            UserId = userId;
            Rank = rank;
            Rating = rating;
        }
    }
}
