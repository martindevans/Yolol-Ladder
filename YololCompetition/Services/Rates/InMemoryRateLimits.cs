using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace YololCompetition.Services.Rates
{
    public class InMemoryRateLimits
        : IRateLimit
    {
        private readonly ConcurrentDictionary<(Guid, ulong), RateLimitState> _used = new ConcurrentDictionary<(Guid, ulong), RateLimitState>();

        public async Task<RateLimitState?> TryGetLastUsed(Guid rateId, ulong userId)
        {
            await Task.CompletedTask;

            if (_used.TryGetValue((rateId, userId), out var value))
                return value;
            return null;
        }

        public Task Use(Guid rateId, ulong userId)
        {
            if (!_used.TryGetValue((rateId, userId), out var value))
                value = new RateLimitState(DateTime.UtcNow, 0);

            _used[(rateId, userId)] = new RateLimitState(DateTime.UtcNow, value.UseCount + 1);
            return Task.CompletedTask;
        }

        public Task Reset(Guid rateId, ulong userId)
        {
            _used.TryRemove((rateId, userId), out _);
            return Task.CompletedTask;
        }
    }
}
