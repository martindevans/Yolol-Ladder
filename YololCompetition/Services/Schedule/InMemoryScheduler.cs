using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Solutions;
using System.Linq;
using Discord.WebSocket;
using YololCompetition.Extensions;
using YololCompetition.Services.Subscription;

namespace YololCompetition.Services.Schedule
{
    public class InMemoryScheduler
        : IScheduler
    {
        private readonly IChallenges _challenges;
        private readonly ISolutions _solutions;
        private readonly ISubscription _subscriptions;
        private readonly DiscordSocketClient _client;

        public InMemoryScheduler(IChallenges challenges, ISolutions solutions, ISubscription subscriptions, DiscordSocketClient client)
        {
            _challenges = challenges;
            _solutions = solutions;
            _subscriptions = subscriptions;
            _client = client;
        }

        public async Task Start()
        {
            while (true)
            {
                // Find the currently running challenge
                var current = await _challenges.GetCurrentChallenge();

                // If there is no challenge running try to start a new one
                if (current == null)
                {
                    Console.WriteLine("There is no current challenge - attempting to start a new one");

                    // Start the next challenge, if there isn't one wait a while
                    var next = await _challenges.StartNext();
                    if (next == null)
                    {
                        Console.WriteLine("No challenges available, waiting for a while...");
                        await Task.Delay(TimeSpan.FromMinutes(30));
                        continue;
                    }

                    Console.WriteLine($"Starting new challenge {next.Name}");

                    // Notify all subscribed channels about the new challenge
                    await NotifyStart(next);
                    current = next;
                }
                else
                    Console.WriteLine($"{current.Name} challenge is currently running");

                //There is a challenge running, wait until the end time
                var endTime = current.EndTime;
                if (endTime != null)
                {
                    var delay = endTime.Value - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay);
                }

                // Finish the current challenge
                await _challenges.EndCurrentChallenge();

                // Get the leaderboard for the challenge that just finished and notify everyone
                await NotifyEnd(current, _solutions.GetSolutions(current.Id, uint.MaxValue));
            }
        }

        private string GetName(ulong id)
        {
            return _client.GetUser(id)?.Username ?? $"User{id}";
        }

        private async Task NotifyEnd(Challenge.Challenge challenge, IAsyncEnumerable<RankedSolution> solutions)
        {
            var top = await solutions.Take(10).ToArrayAsync();
            var (othersCount, othersScore) = await solutions.Skip(10).Select(a => (1, a.Solution.Score)).AggregateAsync((0u, 0L), (a, b) => ((uint)(a.Item1 + b.Item1), a.Item1 + b.Score));

            EmbedBuilder embed = new EmbedBuilder {
                Title = $"Competition `{challenge.Name}` Complete!",
                Color = Color.Blue,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };
            if (top.Length == 0)
            {
                embed.Description = "Challenge ended with no entries.";
            }
            else
            {
                embed.Description = $"**{GetName(top[0].Solution.UserId)}** is victorious with a score of **{top[0].Solution.Score}**";

                var leaderboardStr = string.Join("\n", top.Select(a => $"{a.Rank}. {GetName(a.Solution.UserId)} **{a.Solution.Score}**"));
                if (othersCount > 0)
                    leaderboardStr += $"\n{othersCount} other entrants scored {othersScore} combined points.";

                embed.AddField("Leaderboard", leaderboardStr);
            }

            await SendEmbedToSubs(embed.Build());
        }

        private async Task NotifyStart(Challenge.Challenge challenge)
        {
            await SendEmbedToSubs(challenge.ToEmbed().Build());
        }

        private async Task SendEmbedToSubs(Embed embed)
        {
            await foreach (var subscription in _subscriptions.GetSubscriptions())
            {
                var channel = _client.GetGuild(subscription.Guild)?.GetTextChannel(subscription.Channel);
                if (channel == null)
                    continue;

                await channel.SendMessageAsync(embed: embed);
                await Task.Delay(100);
            }
        }
    }
}
