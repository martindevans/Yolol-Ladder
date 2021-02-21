using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using YololCompetition.Attributes;
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

        private async Task SubmitSolution(Challenge challenge, string program, bool save)
        {
            var code = program.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var (success, failure) = await _verification.Verify(challenge, code).ConfigureAwait(false);
            if (failure != null)
            {
                var message = failure.Type switch {
                    FailureType.ParseFailed => $"Code is not valid Yolol code: ```{failure.Hint}```",
                    FailureType.RuntimeTooLong => $"Program took too long to produce a result. {failure.Hint}",
                    FailureType.IncorrectResult => $"Program produced an incorrect value! {failure.Hint}",
                    FailureType.ProgramTooLarge => "Program is too large - it must be 20 lines by 70 characters per line",
                    FailureType.InvalidProgramForChipType => $"Program used a feature which is not available on this level of Yolol chip. {failure.Hint}",
                    FailureType.ChallengeCodeFailed => $"Challenge code crashed! Please contact Martin#2468. {failure.Hint}",
                    FailureType.ChallengeForceFail => $"{failure.Hint}",
                    FailureType.Other => failure.Hint,
                    _ => throw new ArgumentOutOfRangeException()
                };

                await ReplyWithHint("Verification Failed!", message);
                return;
            }

            if (success == null)
                throw new InvalidOperationException("Failed to verify solution (this is a bug, please contact @Martin#2468)");

            await SubmitAndReply(success, new Solution(challenge.Id, Context.User.Id, success.Score, code), save);
        }

        private async Task SubmitAndReply(Success verification, Solution submission, bool save)
        {
            var previous = await _solutions.GetSolution(Context.User.Id, submission.ChallengeId);

            // Handle submitting something that scores worse than your existing solution
            if (previous.HasValue && previous.Value.Score > submission.Score)
            {
                await ReplyAsync($"Verification complete! You score {verification.Score} points using {verification.Length} chars and {verification.Iterations} ticks. Less than your current best of {previous.Value.Score}");
                if (verification.Hint != null)
                    await ReplyAsync(verification.Hint);
                return;
            }

            // This submission was better than previous, but saving is not enabled (i.e. submitting to an old competition). Reply with score and early out.
            if (!save)
            {
                await ReplyAsync($"Verification complete! You scored {verification.Score} points using {verification.Length} chars and {verification.Iterations} ticks.");
                if (verification.Hint != null)
                    await ReplyAsync(verification.Hint);
                return;
            }

            // Submission was better than previous and saving is enabled.
            // We might need to do a rank alert!

            // Get the current top solution(s)
            var topSolutionsBefore = await _solutions.GetTopRank(submission.ChallengeId).Select(a => a.Solution).ToListAsync();
            var topUsersBefore = topSolutionsBefore.Select(a => a.UserId).ToList();

            // Save this solution and reply to user with result
            await _solutions.SetSolution(submission);
            var rank = await _solutions.GetRank(submission.ChallengeId, Context.User.Id);
            await ReplyAsync($"Verification complete! You scored {verification.Score} points using {verification.Length} chars and {verification.Iterations} ticks. You are currently rank {rank?.Rank} for this challenge.");
            if (verification.Hint != null)
                await ReplyAsync(verification.Hint);

            // There should always be a rank after the call to `SetSolution`. if there isn't just early out here ¯\_(ツ)_/¯
            if (!rank.HasValue)
                return;

            // If this is not the top ranking score, or there was no top ranking score before there is no need to put out a rank alert.
            if (rank.Value.Rank != 1 || topUsersBefore.Count == 0)
                return;
            var topScoreBefore = topSolutionsBefore[0].Score;

            // There are three possible rank alerts:
            // 1) User moved from below to above: "X takes rank #1 from Y"
            // 2) User moved from below to tie: "X ties for rank #1"
            // 3) User moved from tie to above: "X breaks a tie to take #1 from Y"

            // Create the embed to fill in with details of the rank alert later
            var embed = new EmbedBuilder {Title = "Rank Alert", Color = Color.Gold, Footer = new EmbedFooterBuilder().WithText("A Cylon Project")};
            var self = await _client.GetUserName(Context.User.Id);

            // Case #1/#2
            if (!topUsersBefore.Contains(Context.User.Id) && submission.Score >= topScoreBefore)
            {
                var prev = (await topUsersBefore.ToAsyncEnumerable().SelectAwait(async a => await _client.GetUserName(a)).ToArrayAsync()).Humanize("&");

                if (submission.Score > topScoreBefore)
                    embed.Description = $"{self} takes rank #1 from {prev}!";
                else if (submission.Score == topScoreBefore)
                    embed.Description = $"{self} ties for rank #1";
                else
                    return;
            }
            else if (topUsersBefore.Contains(Context.User.Id) && topUsersBefore.Count > 1 && submission.Score > topScoreBefore)
            {
                topUsersBefore.Remove(Context.User.Id);
                var prev = (await topUsersBefore.ToAsyncEnumerable().SelectAwait(async a => await _client.GetUserName(a)).ToArrayAsync()).Humanize("&");

                embed.Description = $"{self} breaks a tie to take #1 from {prev}!";
            }
            else
                return;

            // Broadcast embed out to all subscribed channels
            await _broadcast.Broadcast(embed.Build()).LastAsync();
        }

        private async Task ReplyWithHint(string prefix, string? hint)
        {
            if (hint != null && prefix.Length + hint.Length > 1000)
            {
                await ReplyAsync(prefix);
                await using (var stream = new MemoryStream(hint.Length))
                await using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(hint);
                    await writer.FlushAsync();
                    stream.Position = 0;

                    await Context.Channel.SendFileAsync(stream, "hint.txt", "Message is too long!");
                }
            }
            else
            {
                await ReplyAsync($"{prefix} {hint}");
            }
        }

        [Command("submit"), Summary("Submit a new competition entry. Code must be enclosed in triple backticks.")]
        [RateLimit("b7083f80-8979-450f-a6ff-e7e5886b038b", 5, "Please wait a short while before submitting another solution")]
        public async Task SubmitSolution([Remainder] string input)
        {
            var challenge = await _challenges.GetCurrentChallenge();
            if (challenge == null)
            {
                await ReplyAsync("There is no currently running challenge!");
                return;
            }

            await SubmitSolution(challenge, input, true);
        }

        [Command("submitto"), Summary("Submit an entry to a previous competition. Code must be enclosed in triple backticks.")]
        [RateLimit("b7083f80-8979-450f-a6ff-e7e5886b038b", 5, "Please wait a short while before submitting another solution")]
        public async Task SubmitSolution(string id, [Remainder] string input)
        {
            var c = await _challenges.FuzzyFindChallenge(id).Take(5).ToArrayAsync();
            if (c.Length > 1)
            {
                await ReplyAsync("Found more than one challenge matching that search string, please be more specific");
            }
            else if (c.Length == 0)
            {
                await ReplyAsync("Could not find a challenge matching that searching string");
            }
            else
            {
                var current = await _challenges.GetCurrentChallenge();
                await SubmitSolution(c[0], input, current?.Id == c[0].Id);
            }
        }
    }
}
