using System;
using Discord;
using Discord.WebSocket;
using Humanizer;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Cron;

namespace YololCompetition.Services.Status
{
    public class SchedulerStatus
        : IStatusUpdater
    {
        private readonly ICron _cron;
        private readonly IChallenges _challenges;
        private readonly DiscordSocketClient _client;

        private const string IdleActivity = ">help";

        public SchedulerStatus(ICron cron, IChallenges challenges, DiscordSocketClient client)
        {
            _cron = cron;
            _challenges = challenges;
            _client = client;
        }

        public void Start()
        {
            var period = TimeSpan.FromMinutes(2);
            _cron.Schedule(TimeSpan.Zero, default, async ct =>
            {
                var c = await _challenges.GetCurrentChallenge();
                var time = c?.EndTime;

                if (time.HasValue)
                {
                    var duration = time.Value - DateTime.UtcNow;
                    if (duration > TimeSpan.Zero)
                    {
                        var text = $"{duration.Humanize()} left on challenge";
                        await _client.SetActivityAsync(new Game(text));

                        // Wait 2 mins or until the current challenge ends, whichever is first
                        return TimeSpan.FromTicks(Math.Min(duration.Ticks, period.Ticks));
                    }
                }
                
                await _client.SetActivityAsync(new Game(IdleActivity));
                return period;
                
            });
        }
    }
}
