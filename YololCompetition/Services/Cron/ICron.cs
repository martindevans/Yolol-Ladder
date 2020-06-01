using System;
using System.Threading;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public interface ICron
    {
        /// <summary>
        /// Schedule a job to run repeatedly after a delay (chosen by itself) until cancelled (potentially by itself)
        /// </summary>
        /// <param name="initialDelay"></param>
        /// <param name="token"></param>
        /// <param name="run"></param>
        Task Schedule(TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<TimeSpan?>> run);
    }
}
