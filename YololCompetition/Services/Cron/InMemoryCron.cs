using System;
using System.Threading;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public class InMemoryCron
        : ICron
    {
        public Task Schedule(TimeSpan period, TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<bool>> run)
        {
            return Task.Run(async () => {

                await Task.Delay(initialDelay, token);

                while (!token.IsCancellationRequested)
                {
                    var cont = await run(token);
                    if (!cont)
                        break;

                    await Task.Delay(period, token);
                }
                
            }, token);
        }

        public Task Schedule(TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<TimeSpan?>> run)
        {
            return Task.Run(async () => {

                await Task.Delay(initialDelay, token);

                while (!token.IsCancellationRequested)
                {
                    var delay = await run(token);
                    if (delay == null)
                        break;

                    await Task.Delay(delay.Value, token);
                }
                
            }, token);
        }
    }
}
