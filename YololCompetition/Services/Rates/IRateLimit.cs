using System;
using System.Threading.Tasks;

namespace YololCompetition.Services.Rates
{
    public interface IRateLimit
    {
        public Task<RateLimitState?> TryGetLastUsed(Guid rateId, ulong userId);

        public Task Use(Guid rateId, ulong userId);

        public Task Reset(Guid rateId, ulong userId);
    }

    public readonly struct RateLimitState
    {
        public DateTime LastUsed { get; }
        public uint UseCount { get; }

        public RateLimitState(DateTime lastUsed, uint useCount)
        {
            LastUsed = lastUsed;
            UseCount = useCount;
        }
    }
}
