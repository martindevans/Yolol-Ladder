using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using YololCompetition.Extensions;
using YololCompetition.Services.Broadcast;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Verification;

namespace YololCompetition.Modules
{
    public class Submit
        : ModuleBase
    {
        private readonly ISolutions _solutions;
        private readonly IChallenges _challenges;
        private readonly IVerification _verification;
        private readonly IBroadcast _broadcast;
        private readonly DiscordSocketClient _client;

        public Submit(ISolutions solutions, IChallenges challenges, IVerification verification, IBroadcast broadcast, DiscordSocketClient client)
        {
            _solutions = solutions;
            _challenges = challenges;
            _verification = verification;
            _broadcast = broadcast;
            _client = client;
        }

        [Command("submit"), Summary("Submit a new competition entry. Code must be enclosed in triple backticks.")]
        public async Task SubmitSolution([Remainder] string input)
        {
            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var challenge = await _challenges.GetCurrentChallenge();
            if (challenge == null)
            {
                await ReplyAsync("There is no currently running challenge!");
                return;
            }

            var (success, failure) = await _verification.Verify(challenge, code);
            if (failure != null)
            {
                var message = failure.Type switch {
                    FailureType.ParseFailed => $"Code is not valid Yolol code: {failure.Hint}",
                    FailureType.RuntimeTooLong => "Program took too long to produce a result",
                    FailureType.IncorrectResult => $"Program produced an incorrect value! {failure.Hint}",
                    FailureType.ProgramTooLarge => "Program is too large - it must be 20 lines by 70 characters per line",
                    _ => throw new ArgumentOutOfRangeException()
                };

                await ReplyAsync($"Verification failed! {message}.");
            }
            else if (success != null)
            {
                var solution = await _solutions.GetSolution(Context.User.Id, challenge.Id);
                if (solution.HasValue && success.Score < solution.Value.Score)
                {
                    await ReplyAsync($"Verification complete! You score {success.Score} points. Less than your current best of {solution.Value.Score}");
                }
                else
                {
                    // Get the current top solution
                    var topBefore = await _solutions.GetTopRank(challenge.Id).ToArrayAsync();

                    // Submit this solution
                    await _solutions.SetSolution(new Solution(challenge.Id, Context.User.Id, success.Score, code));
                    var rank = await _solutions.GetRank(challenge.Id, Context.User.Id);
                    var rankNum = uint.MaxValue;
                    if (rank.HasValue)
                        rankNum = rank.Value.Rank;
                    await ReplyAsync($"Verification complete! You scored {success.Score} points. You are currently rank {rankNum} for this challenge.");

                    // If this is the top ranking score, and there was a top ranking score before, and it wasn't this user: alert everyone
                    if (rankNum == 1 && topBefore.Length > 0 && topBefore.All(a => a.Solution.UserId != Context.User.Id))
                    {
                        var embed = new EmbedBuilder {
                            Title = "Rank Alert",
                            Color = Color.Gold,
                            Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
                        };

                        var self = await _client.GetUserName(Context.User.Id);
                        var prev = (await topBefore.ToAsyncEnumerable().SelectAwait(async a => await _client.GetUserName(a.Solution.UserId)).ToArrayAsync()).Humanize("&");

                        embed.Description = success.Score == topBefore[0].Solution.Score
                            ? $"{self} ties for rank #1"
                            : $"{self} takes rank #1 from {prev}!";

                        await _broadcast.Broadcast(embed.Build());
                    }
                }
            }
            else
                throw new InvalidOperationException("Failed to verify solution (this is a bug, please contact @Martin#2468)");
        }
    }
}
