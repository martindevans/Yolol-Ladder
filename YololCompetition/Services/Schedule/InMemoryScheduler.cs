using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Solutions;
using System.Linq;
using System.Text;
using BalderHash.Extensions;
using Discord.WebSocket;
using Nito.AsyncEx;
using YololCompetition.Extensions;
using YololCompetition.Services.Broadcast;
using YololCompetition.Services.Leaderboard;
using YololCompetition.Services.Messages;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Services.Schedule
{
    public class InMemoryScheduler
        : IScheduler
    {
        private readonly IChallenges _challenges;
        private readonly ISolutions _solutions;
        private readonly IBroadcast _broadcaster;
        private readonly ILeaderboard _leaderboard;
        private readonly DiscordSocketClient _client;
        private readonly IMessages _messages;
        private readonly ITrueskillUpdater _skillUpdate;
        private readonly Configuration _config;

        private readonly AsyncAutoResetEvent _poker = new();

        public SchedulerState State { get; private set; }

        public InMemoryScheduler(IChallenges challenges, ISolutions solutions, IBroadcast broadcaster, ILeaderboard leaderboard, DiscordSocketClient client, IMessages messages, ITrueskillUpdater skillUpdate, Configuration config)
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
            while (true)
            {
                State = SchedulerState.StartingChallenge;

                // Find the currently running challenge
                var current = await _challenges.GetCurrentChallenge();

                // If there is no challenge running try to start a new one
                if (current == null)
                {
                    Console.WriteLine("There is no current challenge - checking if challenge finished within the last 24 hours");

                    var currentTime = DateTime.UtcNow;
                    var startTime = currentTime.Date + TimeSpan.FromMinutes(_config.ChallengeStartTime);  //Gets the time today that the challenge would be starting if it were starting today
                    var searchTime = currentTime - TimeSpan.FromDays(1);
                    var recent = await _challenges.GetChallengesByEndTime(searchTime.UnixTimestamp()).FirstOrDefaultAsync();

                    if (recent == null)
                    {

                        // Start the next challenge, if there isn't one wait a while
                        var next = await _challenges.StartNext();
                        if (next == null)
                        {
                            Console.WriteLine("No challenges available, waiting for a while...");
                            State = SchedulerState.WaitingNoChallengesInPool;
                            await Task.Delay(TimeSpan.FromMinutes(1));
                            continue;
                        }

                        Console.WriteLine($"Starting new challenge {next.Name}");

                        // Notify all subscribed channels about the new challenge
                        await NotifyStart(next);
                        current = next;
                    }
                    else 
                    {

                        // Wait for the next start time.
                        if (currentTime > startTime)
                        {

                            //If its after the start time today, push it til tomorrow
                            startTime += TimeSpan.FromDays(1); 
                        }

                        Console.WriteLine("Challenge ended within last 24 hours, waiting for next start time");
                        State = SchedulerState.WaitingCooldown;
                        var waitTime = startTime - currentTime; //wait until the proper time to start a challenge
                        await Task.WhenAny(_poker.WaitAsync(), Task.Delay(waitTime));
                        continue;
                    }
                }
                else
                    Console.WriteLine($"{current.Name} challenge is currently running");

                //There is a challenge running, wait until the end time or until someone externally pokes us awake
                var endTime = current.EndTime;
                while (endTime != null && endTime > DateTime.UtcNow)
                {
                    var delay = endTime.Value - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        State = SchedulerState.WaitingChallengeEnd;
                        await Task.WhenAny(_poker.WaitAsync(), Task.Delay(delay));
                    }
                }

                // Refetch current challenge in case it has been updated
                current = await _challenges.GetCurrentChallenge();
                if (current == null)
                    continue;
                endTime = current.EndTime;

                // If endtime is after now then something else poked the scheduler awake, reset scheduler logic
                if (endTime != null && endTime > DateTime.UtcNow)
                    continue;

                State = SchedulerState.EndingChallenge;

                // Finish the current challenge
                await _challenges.EndCurrentChallenge();

                // Transfer scores to leaderboard
                await UpdateLeaderboard(current, _solutions.GetSolutions(current.Id, uint.MaxValue));

                // Get the leaderboard for the challenge that just finished and notify everyone
                await NotifyEnd(current, _solutions.GetSolutions(current.Id, uint.MaxValue));

                // Wait for a cooldown period
                State = SchedulerState.WaitingCooldown;
                await Task.WhenAny(_poker.WaitAsync(), Task.Delay(TimeSpan.FromHours(24)));
            }
            

            // ReSharper disable once FunctionNeverReturns
            // Justification: We never want the scheduler to stop!
        }

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
                    await _leaderboard.AddScore((rank.Single()).Solution.UserId, (uint)challenge.Difficulty);

                // Award at least one point to every entrant
                if (score > 1)
                    score--;
            }

            // Find the smallest solution, if there's only one of them (i.e. no tie for smallest) award a bonus point
            var smallestGroup = solutions.GroupBy(a => a.Solution.Yolol.Length).Aggregate((a, b) => a.Key < b.Key ? a : b);
            if (smallestGroup.Count() == 1)
                await _leaderboard.AddScore((smallestGroup.First()).Solution.UserId, (uint)challenge.Difficulty);
        }

        private async Task NotifyEnd(Challenge.Challenge challenge, IAsyncEnumerable<RankedSolution> solutions)
        {
            var top = await solutions.Take(10).ToArrayAsync();
            if (top.Length == 0)
                return;

            var (othersCount, othersScore) = await solutions.Skip(10).Select(a => (1, a.Solution.Score)).AggregateAsync((0u, 0L), (a, b) => ((uint)(a.Item1 + b.Item1), a.Item1 + b.Score));

            EmbedBuilder embed = new() {
                Title = $"Competition `{challenge.Name}` Complete!",
                Color = Color.Blue,
                Footer = new EmbedFooterBuilder().WithText($"{((uint)challenge.Id).BalderHash()} - A Cylon Project ({DateTime.UtcNow.Ticks.GetHashCode()})")
            };
            if (top.Length == 0)
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

        public Task Poke()
        {
            _poker.Set();

            return Task.CompletedTask;
        }
    }
}
