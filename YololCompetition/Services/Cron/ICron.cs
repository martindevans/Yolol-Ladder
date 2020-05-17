using System;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public interface ICron
    {
        void Schedule(TimeSpan period, TimeSpan initialDelay, uint maxRuns, Func<Task<bool>> run);
    }
}
