using System;
using System.Threading;
using System.Threading.Tasks;

namespace YololCompetition.Services.Cron
{
    public class InMemoryCron
        : ICron
    {
        public Task Schedule(TimeSpan initialDelay, CancellationToken token, Func<CancellationToken, Task<TimeSpan?>> run)
        {
            return Task.Run(async () => {

                await Task.Delay(initialDelay, token);

                while (!token.IsCancellationRequested)
                {
                    TimeSpan? delay = null;
                    try
                    {
                        delay = await run(token);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Cron job Exception: {e}");
                    }

                    if (delay == null)
                        break;
                    await Task.Delay(delay.Value, token);
                }
                
            }, token);
        }
    }
}
