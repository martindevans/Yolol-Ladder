using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace YololCompetition.Services.Rates
{
    public class InMemoryRateLimits
        : IRateLimit
    {
        private ConcurrentDictionary<(Guid, ulong), DateTime> _used = new ConcurrentDictionary<(Guid, ulong), DateTime>();

        public async Task<DateTime?> TryGetLastUsed(Guid rateId, ulong userId)
        {
            await Task.CompletedTask;

            if (_used.TryGetValue((rateId, userId), out var value))
                return value;
            return null;
        }

        public Task Use(Guid rateId, ulong userId)
        {
            _used[(rateId, userId)] = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }
}
