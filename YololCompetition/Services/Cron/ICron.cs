using System;
using System.Threading;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public interface ICron
    {
        /// <summary>
        /// Schedule a job to run periodically until cancelled (potentially by itself)
        /// </summary>
        /// <param name="period"></param>
        /// <param name="initialDelay"></param>
        /// <param name="token"></param>
        /// <param name="run"></param>
        Task Schedule(TimeSpan period, TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<bool>> run);

        /// <summary>
        /// Schedule a job to run repeatedly after a delay (chosen by itself) until cancelled (potentially by itself)
        /// </summary>
        /// <param name="initialDelay"></param>
        /// <param name="token"></param>
        /// <param name="run"></param>
        Task Schedule(TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<TimeSpan?>> run);
    }

    public static class ICronExtensions
    {
        public static void Schedule(this ICron cron, TimeSpan period, TimeSpan initialDelay, uint maxRuns, Func<Task<bool>> run)
        {
            uint runs = 0;
            cron.Schedule(period, initialDelay, default, async _ => {
                runs++;
                if (runs > maxRuns)
                    return false;
                return await run();
            });
        }
    }
}
