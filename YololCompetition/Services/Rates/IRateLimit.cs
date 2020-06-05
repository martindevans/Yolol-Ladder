using System;
using System.Threading.Tasks;

namespace YololCompetition.Services.Rates
{
    public interface IRateLimit
    {
        public Task<DateTime?> TryGetLastUsed(Guid rateId, ulong userId);

        public Task Use(Guid rateId, ulong userId);
    }
}
