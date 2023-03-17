using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BalderHash.Extensions;
using Discord;
using Discord.WebSocket;
using Nito.AsyncEx;
using YololCompetition.Extensions;
using YololCompetition.Services.Broadcast;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Leaderboard;
using YololCompetition.Services.Messages;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Services.Schedule
{
    public class Scheduler2
        : IScheduler
    {
        private readonly IChallenges _challenges;
        private readonly ISolutions _solutions;
        private readonly IBroadcast _broadcaster;
        private readonly DiscordSocketClient _client;
        private readonly IMessages _messages;
        private readonly ITrueskillUpdater _skillUpdate;
        private readonly ILeaderboard _leaderboard;
        private readonly Configuration _config;

        private readonly AsyncAutoResetEvent _poker = new();

        public SchedulerState State { get; private set; }

        public Scheduler2(IChallenges challenges, ISolutions solutions, IBroadcast broadcaster, ILeaderboard leaderboard, DiscordSocketClient client, IMessages messages, ITrueskillUpdater skillUpdate, Configuration config)
        {
            _challenges = challenges;
            _solutions = solutions;
            _broadcaster = broadcaster;
            _client = client;
            _messages = messages;
            _skillUpdate = skillUpdate;
            _leaderboard = leaderboard;
            _config = config;
        }

        public async Task Start()
        {
            State = await DiscoverInitialState();

            // Keep looping forever, applying state transitions returned by each state handler method.
            while (true)
            {
                State = await (State switch
                {
                    SchedulerState.StartingChallenge => StartingChallenge(),
                    SchedulerState.WaitingNoChallengesInPool => WaitingNoChallengesInPool(),
                    SchedulerState.WaitingChallengeEnd => WaitingChallengeEnd(),
                    SchedulerState.EndingChallenge => EndingChallenge(),
                    SchedulerState.WaitingCooldown => WaitingCooldown(),
                    _ => throw new ArgumentOutOfRangeException()
                });
            }
        }

        private async Task<SchedulerState> StartingChallenge()
        {
            // Start the next challenge, if there isn't one wait a while
            var next = await _challenges.StartNext();
            if (next == null)
                return SchedulerState.WaitingNoChallengesInPool;

            Console.WriteLine($"Starting new challenge {next.Name}");

            // Notify all subscribed channels about the new challenge
            await NotifyStart(next);

            return SchedulerState.WaitingChallengeEnd;
        }

        private async Task<SchedulerState> WaitingCooldown()
        {
            var currentTime = DateTime.UtcNow;

            // Get challenges that ended within the last 23 hours
            var searchTime = currentTime - TimeSpan.FromHours(23);
            var recent = await _challenges.GetChallengesByEndTime(searchTime.UnixTimestamp()).FirstOrDefaultAsync();

            // If there was one, wait a while
            if (recent != null)
            {
                await Task.WhenAny(_poker.WaitAsync(), Task.Delay(TimeSpan.FromHours(1)));
                return SchedulerState.WaitingCooldown;
            }

            // Calculate the appropriate time for a challenge to start today (or tomorrow, if we've already missed the window)
            var startTimeToday = currentTime.Date + TimeSpan.FromMinutes(_config.ChallengeStartTime);
            if (currentTime > startTimeToday)
                startTimeToday += TimeSpan.FromDays(1);

            // Wait for the start time
            await Task.WhenAny(_poker.WaitAsync(), Task.Delay(startTimeToday - currentTime));

            // It's possible we were woken up very early, or slightly late. Start challenge if we're within 5 minutes of the expected time
            currentTime = DateTime.UtcNow;
            var a = currentTime - TimeSpan.FromMinutes(_config.ChallengeStartTime);
            var b = currentTime.Date;
            if (Math.Abs((a - b).TotalMinutes) < 5)
                return SchedulerState.StartingChallenge;
            return SchedulerState.WaitingCooldown;
        }

        private async Task<SchedulerState> WaitingNoChallengesInPool()
        {
            // Wait until there is a pending challenge
            while (true)
            {
                var pending = await _challenges.GetPendingCount();
                if (pending > 0)
                    break;
                await Task.WhenAny(_poker.WaitAsync(), Task.Delay(TimeSpan.FromHours(1)));
            }

            // Wait for the cooldown since the last challenge to expire
            return SchedulerState.WaitingCooldown;
        }

        private async Task<SchedulerState> EndingChallenge()
        {
            var current = await _challenges.GetCurrentChallenge();
            if (current == null)
                return SchedulerState.WaitingCooldown;

            // Finish the current challenge
            await _challenges.EndCurrentChallenge();

            // Transfer scores to leaderboard
            await UpdateLeaderboard(current, _solutions.GetSolutions(current.Id, uint.MaxValue));

            // Get the leaderboard for the challenge that just finished and notify everyone
            await NotifyEnd(current, _solutions.GetSolutions(current.Id, uint.MaxValue));

            return SchedulerState.WaitingCooldown;
        }

        private async Task<SchedulerState> DiscoverInitialState()
        {
            var current = await _challenges.GetCurrentChallenge();
            return current != null
                ? SchedulerState.WaitingChallengeEnd
                : SchedulerState.WaitingCooldown;
        }

        private async Task<SchedulerState> WaitingChallengeEnd()
        {
            var current = await _challenges.GetCurrentChallenge();

            // Wait until end time (or an explicit poke wakes us up)
            var endTime = current?.EndTime;
            while (endTime != null && endTime > DateTime.UtcNow)
            {
                var delay = endTime.Value - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.WhenAny(_poker.WaitAsync(), Task.Delay(delay));
            }

            return SchedulerState.EndingChallenge;
        }

        #region starting/ending helpers
        private async Task UpdateLeaderboard(Challenge.Challenge challenge, IAsyncEnumerable<RankedSolution> solutionsAsync)
        {
            var solutions = await solutionsAsync.ToListAsync();
            if (solutions.Count == 0)
                return;

            // Update trueskill
            await _skillUpdate.ApplyChallengeResults(challenge.Id);

            const uint maxScore = 10;
            var score = maxScore;

            // Enumerate through each group of equally ranked users
            var ranks = solutions.GroupBy(a => a.Rank).OrderBy(a => a.Key);
            foreach (var rank in ranks)
            {
                // Award all users at the same rank some points
                var count = 0;
                foreach (var solution in rank)
                {
                    count++;
                    await _leaderboard.AddScore(solution.Solution.UserId, score * (uint)challenge.Difficulty);
                }

                // If there was only one user in the top rank, award them a bonus
                if (count == 1 && score == maxScore)
                    await _leaderboard.AddScore(rank.Single().Solution.UserId, (uint)challenge.Difficulty);

                // Award at least one point to every entrant
                if (score > 1)
                    score--;
            }

            // Find the smallest solution, if there's only one of them (i.e. no tie for smallest) award a bonus point
            var smallestGroup = solutions.GroupBy(a => a.Solution.Yolol.Length).Aggregate((a, b) => a.Key < b.Key ? a : b);
            if (smallestGroup.Count() == 1)
                await _leaderboard.AddScore(smallestGroup.First().Solution.UserId, (uint)challenge.Difficulty);
        }

        private async Task NotifyEnd(Challenge.Challenge challenge, IAsyncEnumerable<RankedSolution> solutionsAsync)
        {
            var solutions = await solutionsAsync.ToListAsync();

            var top = solutions.Take(10).ToList();
            if (top.Count == 0)
                return;

            var (othersCount, othersScore) = solutions
                .Skip(10)
                .Select(a => (1, a.Solution.Score))
                .Aggregate((0u, 0L), (a, b) => ((uint)(a.Item1 + b.Item1), a.Item1 + b.Score));

            EmbedBuilder embed = new()
            {
                Title = $"Competition `{challenge.Name}` Complete!",
                Color = Color.Blue,
                Footer = new EmbedFooterBuilder().WithText($"{((uint)challenge.Id).BalderHash()} - A Cylon Project ({DateTime.UtcNow.Ticks.GetHashCode()})")
            };
            if (top.Count == 0)
            {
                embed.Description = "Challenge ended with no entries.";
            }
            else
            {
                embed.Description = $"**{await _client.GetUserName(top[0].Solution.UserId)}** is victorious with a score of **{top[0].Solution.Score}**\n\n```{top[0].Solution.Yolol}```";

                var leaderboardStr = new StringBuilder();
                foreach (var item in top)
                    leaderboardStr.AppendLine($"{item.Rank}. {await _client.GetUserName(item.Solution.UserId)} **{item.Solution.Score}**");
                if (othersCount > 0)
                    leaderboardStr.AppendLine($"\n{othersCount} other entrants scored {othersScore} combined points.");

                embed.AddField("Leaderboard", leaderboardStr.ToString());
            }

            await SendEmbedToSubs(embed.Build(), null);
        }

        private async Task NotifyStart(Challenge.Challenge challenge)
        {
            await SendEmbedToSubs(challenge.ToEmbed().Build(), challenge.Id);
        }

        private async Task SendEmbedToSubs(Embed embed, ulong? challengeId)
        {
            var messages = new List<IUserMessage>();

            await foreach (var message in _broadcaster.Broadcast(embed))
            {
                await Task.Delay(100);
                if (challengeId != null)
                    messages.Add(message);
            }

            if (challengeId != null)
                foreach (var message in messages)
                    await _messages.TrackMessage(message, challengeId.Value, Messages.MessageType.Current);
        }
        #endregion

        public Task Poke()
        {
            _poker.Set();
            return Task.CompletedTask;
        }

        
    }
}
