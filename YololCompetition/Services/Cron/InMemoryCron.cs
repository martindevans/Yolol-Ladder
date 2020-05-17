using System;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public class InMemoryCron
        : ICron
    {
        public void Schedule(TimeSpan period, TimeSpan initialDelay, uint maxRuns, Func<Task<bool>> run)
        {
            Task.Run(async () => {

                await Task.Delay(initialDelay);

                for (uint i = 0; i < maxRuns; i++)
                {
                    var cont = await run();
                    if (!cont)
                        break;

                    await Task.Delay(period);
                }
            });
        }
    }
}
