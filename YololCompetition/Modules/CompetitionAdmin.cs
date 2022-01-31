using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using Yolol.Execution;
using YololCompetition.Serialization.Json;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Schedule;
using System.Linq;
using BalderHash.Extensions;
using Discord;
using Discord.WebSocket;
using YololCompetition.Extensions;
using YololCompetition.Services.Parsing;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Verification;
using System.Net.Http;
using System.Text;
using JetBrains.Annotations;
using YololCompetition.Services.Interactive;

namespace YololCompetition.Modules
{
    [RequireOwner]
    [UsedImplicitly]
    public class CompetitionAdmin
        : ModuleBase
    {
        private readonly IChallenges _challenges;
        private readonly IScheduler _scheduler;
        private readonly IVerification _verification;
        private readonly DiscordSocketClient _client;
        private readonly IYololParser _parser;
        private readonly IInteractive _interactive;
        private readonly ISolutions _solutions;

        public CompetitionAdmin(IChallenges challenges, IScheduler scheduler, ISolutions solutions, IVerification verification, DiscordSocketClient client, IYololParser parser, IInteractive interactive)
        {
            _challenges = challenges;
            _scheduler = scheduler;
            _verification = verification;
            _client = client;
            _parser = parser;
            _interactive = interactive;
            _solutions = solutions;
        }

        private async Task<string> NextMessageAsync()
        {
#pragma warning disable CS8602
            return (await _interactive.NextMessageAsync(Context.User, Context.Channel, TimeSpan.FromMilliseconds(-1)).ConfigureAwait(false)).Content;
#pragma warning restore CS8602
        }

        [Command("create"), Summary("Create a new challenge")]
        public async Task Create()
        {
            await ReplyAsync("What is the challenge title?");
            var title = await NextMessageAsync();

            await ReplyAsync("What is the challenge description?");
            var desc = await NextMessageAsync();

            var levels = string.Join(',', Enum.GetNames(typeof(ChallengeDifficulty)));
            await ReplyAsync($"What is the challenge difficulty ({levels})?");
            var difficulty = Enum.Parse<ChallengeDifficulty>(await NextMessageAsync());

            await ReplyAsync("What's the Challenge code?");
            var code = await NextMessageAsync();
            var (parseOk, parseErr) = await _parser.Parse(code);
            if (parseOk == null || parseErr != null)
            {
                await ReplyAsync("Parse Error! Aborting challenge creation");
                await ReplyAsync(parseErr ?? "Null Error");
                return;
            }

            await ReplyAsync("What is the challenge URL (raw JSON)?");
            var url = await NextMessageAsync();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var datasetUri))
            {
                await ReplyAsync("Invalid URL format");
                return;
            }

            if (datasetUri.Host != "gist.githubusercontent.com")
            {
                await ReplyAsync("URL must begin with `gist.githubusercontent.com`");
                return;
            }

            Data? data;
            try
            {
                using var hc = new HttpClient();
                var json = await hc.GetStringAsync(datasetUri);
                data = JsonConvert.DeserializeObject<Data>(json, new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] {
                        new YololValueConverter()
                    },
                    FloatParseHandling = FloatParseHandling.Decimal
                });
            }
            catch (Exception e)
            {
                await ReplyAsync($"Failed: {e.Message[..Math.Min(1000, e.Message.Length)]}");
                return;
            }

            if (data == null)
            {
                await ReplyAsync("Test cases cannot be null");
                return;
            }

            if (data.In == null)
            {
                await ReplyAsync("Input values cannot be null");
                return;
            }

            if (data.Out == null)
            {
                await ReplyAsync("Output values cannot be null");
                return;
            }

            await ReplyAsync("Do you want to create this challenge (yes/no)?");
            var confirm = await NextMessageAsync();
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Cancelled creating challenge!");
                return;
            }

            var c = new Challenge(
                0,
                title,
                "done",
                new(data.In),
                new(data.Out),
                null,
                difficulty,
                desc,
                data.Shuffle ?? true,
                data.Mode ?? ScoreMode.BasicScoring,
                data.Chip ?? YololChip.Professional,
                new(parseOk),
                ChallengeStatus.TestMode
            );

            await _challenges.Create(c);
            await ReplyAsync("Challenge has been created in test mode. Use `>promote $challengeid` to add it to the queue");
        }

        [Command("promote"), Summary("Promote challenge from test mode")]
        public async Task Promote(string id)
        {
            var uid = BalderHash.BalderHash32.Parse(id);
            if (!uid.HasValue)
            {
                await ReplyAsync($"Cannot parse `{id}` as a challenge ID");
                return;
            }

            var challenges = await _challenges.GetChallenges(id: uid.Value.Value, includeUnstarted: true).ToArrayAsync();
            if (challenges.Length == 0)
            {
                await ReplyAsync("Cannot find challenge with given ID");
                return;
            }

            await ReplyAsync("Found challenges:");
            foreach (var challenge in challenges)
            {
                await ReplyAsync($" - {challenge.Name} (`{((uint)challenge.Id).BalderHash()}`)");
                await Task.Delay(10);
            }
            await ReplyAsync("Promote those challenges (yes/no)?");
            var confirm = await NextMessageAsync();
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not promoting anything");
                return;
            }

            foreach (var challenge in challenges)
            {
                await _challenges.SetToPending(challenge.Id);
                await Task.Delay(10);
            }

            await ReplyAsync("Done.");
        }

        private class Data
        {
            [JsonProperty("in")]
            public Dictionary<string, Value>[]? In { get; set; }

            [JsonProperty("out")]
            public Dictionary<string, Value>[]? Out { get; set; }

            [JsonProperty("shuffle")]
            public bool? Shuffle { get; set; }

            [JsonProperty("mode")]
            public ScoreMode? Mode { get; set; }

            [JsonProperty("chip")]
            public YololChip? Chip { get; set; }

            [JsonProperty("code")]
            public string? Code { get; set; }
        }

        [Command("show-pool"), Summary("Show state of challenge pool")]
        public async Task ShowPool()
        {
            var none = true;
            await foreach (var challenge in _challenges.GetChallenges(includeUnstarted: true).Where(a => a.EndTime == null))
            {
                none = false;
                if (!challenge.Intermediate.IsOk)
                {
                    await ReplyAsync($" - {challenge.Name} (`{((uint)challenge.Id).BalderHash()}`) - Intermediate code is broken!");
                    await ReplyAsync($"```{challenge.Intermediate.Err}```");
                }
                else
                    await ReplyAsync($" - {challenge.Name} (`{((uint)challenge.Id).BalderHash()}`) {(challenge.Status == ChallengeStatus.TestMode ? "**TEST MODE**" : "")}");
                await Task.Delay(25);
            }

            if (none)
                await ReplyAsync("No challenges in pool :(");
        }

        [Command("delete-challenge"), Summary("Delete a pending challenge from the pool")]
        public async Task DeleteChallenge(string id)
        {
            var uid = BalderHash.BalderHash32.Parse(id);
            if (!uid.HasValue)
            {
                await ReplyAsync($"Cannot parse `{id}` as a challenge ID");
                return;
            }

            var challenges = await _challenges.GetChallenges(id: uid.Value.Value, includeUnstarted: true).ToArrayAsync();
            if (challenges.Length == 0)
            {
                await ReplyAsync("Cannot find challenge with given ID");
                return;
            }

            await ReplyAsync("Found challenges:");
            foreach (var challenge in challenges)
            {
                await ReplyAsync($" - {challenge.Name} (`{((uint)challenge.Id).BalderHash()}`)");
                await Task.Delay(10);
            }
            await ReplyAsync("Delete those challenges (yes/no)?");
            var confirm = await NextMessageAsync();
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not deleting anything");
                return;
            }

            foreach (var challenge in challenges)
            {
                await _challenges.Delete(challenge.Id);
                await Task.Delay(10);
            }

            await ReplyAsync("Done.");
        }

        [Command("terminate-current-challenge"), Summary("Immediately terminate current challenge without scoring")]
        public async Task AbruptEnd()
        {
            await _challenges.EndCurrentChallenge();
            await _scheduler.Poke();
        }

        [Command("replace-challenge-code")]
        public async Task ReplaceChallengeCode(string id, [Remainder] string input)
        {
            var c = await _challenges.FuzzyFindChallenge(id, true).Take(5).ToArrayAsync();
            if (c.Length > 1)
            {
                await ReplyAsync("Found more than one challenge matching that search string, please be more specific");
                return;
            }
            
            if (c.Length == 0)
            {
                await ReplyAsync("Could not find a challenge matching that searching string");
                return;
            }

            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var (program, error) = await _parser.Parse(input);
            if (program == null)
            {
                await ReplyAsync(error);
                return;
            }

            var challenge = c[0];
            challenge.Intermediate = new(program);
            await _challenges.Update(challenge);
            await ReplyAsync("Updated");
        }

        [Command("set-current-difficulty"), Summary("Change difficulty rating of current challenges")]
        public async Task SetDifficulty(ChallengeDifficulty difficulty)
        {
            var current = await _challenges.GetCurrentChallenge();
            if (current == null)
            {
                await ReplyAsync("There is no current challenge");
            }
            else
            {
                await _challenges.ChangeChallengeDifficulty(current, difficulty);
                await ReplyAsync($"Changed difficulty from `{current.Difficulty}` to `{difficulty}`");
            }
        }

        [Command("extend-current"), Summary("Extend current challenge (specify time in hours)")]
        public async Task Extend(int hours)
        {
            var current = await _challenges.GetCurrentChallenge();
            if (current == null)
            {
                await ReplyAsync("No current challenge running");
                return;
            }

            current.EndTime = (current.EndTime ?? DateTime.UtcNow) + TimeSpan.FromHours(hours);
            await _challenges.Update(current);
            await ReplyAsync("Updated");

            if (hours < 0)
                await ReplyAsync("Reduced challenge length requires a bot restart");
        }

        [Command("remove-entry"), Summary("Remove the entry in the current competition for a user")]
        public async Task RemoveEntry(IUser user)
        {
            var current = await _challenges.GetCurrentChallenge();
            if (current == null)
            {
                await ReplyAsync("No challenge is currently running");
                return;
            }

            var rows = await _solutions.DeleteSolution(current.Id, user.Id);
            await ReplyAsync($"Deleted {rows} rows.");
        }

        [Command("crater")]
        public async Task Crater(bool fast = false)
        {
            var challenges = await _challenges.GetChallenges().ToListAsync();
            await ReplyAsync($"Running crater for {challenges.Count} challenges");

            var totalCount = 0;
            var totalFail = 0;

            foreach (var challenge in challenges)
            {
                await using var progress = new DiscordProgressBar($" ## Crater: {challenge.Name}", await ReplyAsync("Crater"));
                await progress.SetProgress(0);

                var fail = 0;
                var count = 0;
                var solutions = await _solutions.GetSolutions(challenge.Id, uint.MaxValue).ToListAsync();
                foreach (var solution in solutions)
                {
                    var (vs, vf) = await _verification.Verify(challenge, solution.Solution.Yolol);
                    if (vs == null)
                    {
                        fail++;
                        totalFail++;
                    }

                    count++;
                    totalCount++;

                    if (!fast)
                    {
                        if (vf != null)
                            await ReplyAsync($" - Failed ({await UserName(solution.Solution.UserId)}): {vf.Hint}".LimitLength(1000));

                        await progress.SetProgress((float)count / solutions.Count);
                        await Task.Delay(250);
                    }
                }

                if (!fast)
                {
                    if (fail > 0)
                        await ReplyAsync($"Failed {fail}/{count} programs\n");
                    await Task.Delay(250);
                }
            }

            await ReplyAsync($"Crater test complete. Failed {totalFail}/{totalCount} programs");
        }

        [Command("rescore"), Summary("Recalculate all scores for a previous competition")]
        public async Task Rescore(string id)
        {
            var uid = BalderHash.BalderHash32.Parse(id);
            if (!uid.HasValue)
            {
                await ReplyAsync($"Cannot parse `{id}` as a challenge ID");
                return;
            }

            var challenges = await _challenges.GetChallenges(id: uid.Value.Value, includeUnstarted: true).ToArrayAsync();
            if (challenges.Length == 0)
            {
                await ReplyAsync("Cannot find challenge with given ID");
                return;
            }

            if (challenges.Length > 1)
            {
                await ReplyAsync("Found more than one challenge, please disambiguate:");
                foreach (var challenge in challenges)
                {
                    await ReplyAsync($" - {challenge.Name} (`{((uint)challenge.Id).BalderHash()}`)");
                    await Task.Delay(10);
                }
                return;
            }

            var c = challenges.Single();
            await ReplyAsync($" - {c.Name} (`{((uint)c.Id).BalderHash()}`)");

            await ReplyAsync("Rescore this challenge (yes/no)?");
            var confirm = await NextMessageAsync();
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not rescoring anything");
                return;
            }

            var solutions = await _solutions.GetSolutions(c.Id, uint.MaxValue).ToArrayAsync();
            const string pbarHeader = "Rescoring: ";
            var progress = await ReplyAsync(pbarHeader);

            var totalTicks = 0L;
            var failures = 0;
            var results = new List<RescoreItem>(solutions.Length);
            for (var i = 0; i < solutions.Length; i++)
            {
                await progress.ModifyAsync(a => a.Content = $"{pbarHeader} {i}/{solutions.Length}");

                var s = solutions[i];
                try
                {
                    var (success, failure) = await _verification.Verify(c, s.Solution.Yolol);

                    if (success != null)
                    {
                        totalTicks += success.Iterations;
                        results.Add(new RescoreItem(
                            s.Solution,
                            new Solution(s.Solution.ChallengeId, s.Solution.UserId, success.Score, s.Solution.Yolol)
                        ));
                    }
                    else if (failure != null)
                    {
                        failures++;
                        results.Add(new RescoreItem(
                            s.Solution,
                            new Solution(s.Solution.ChallengeId, s.Solution.UserId, s.Solution.Score, s.Solution.Yolol),
                            failure.Hint
                        ));
                    }
                    else
                        throw new InvalidOperationException("Verification did not return success or failure");
                }
                catch (InvalidProgramException)
                {
                    await ReplyAsync($"## Invalid Program Exception!\n```{s.Solution.Yolol}```");
                    throw;
                }

                await Task.Delay(100);
            }

            await progress.ModifyAsync(a => a.Content = $"Completed rescoring ({totalTicks} total ticks)");

            if (failures > 0)
                await ReplyAsync($"{failures} solutions failed to verify");

            var report = new StringBuilder();
            report.AppendLine($"Rescoring `{c.Name}`");
            foreach (var rescore in results)
            {
                if (rescore.Failure != null)
                    report.AppendLine($"{await UserName(rescore.Before.UserId)}: {rescore.Before.Score} => {rescore.Failure}");
                else
                    report.AppendLine($"{await UserName(rescore.Before.UserId)}: {rescore.Before.Score} => {rescore.After!.Value.Score}");
            }

            await ReplyAsync(report.ToString());

            await ReplyAsync("Apply rescoring to this challenge (yes/no)?");
            var confirm2 = await NextMessageAsync();
            if (!confirm2.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not applying rescoring");
                return;
            }

            foreach (var rescore in results)
            {
                await _solutions.DeleteSolution(rescore.Before.ChallengeId, rescore.Before.UserId);

                if (rescore.After.HasValue)
                    await _solutions.SetSolution(rescore.After.Value);
            }

            await ReplyAsync("Done.");
        }

        private readonly struct RescoreItem
        {
            public readonly Solution Before;
            public readonly Solution? After;
            public readonly string? Failure;

            public RescoreItem(Solution before, Solution after, string? failure = null)
            {
                Before = before;
                After = after;
                Failure = failure;
            }
        }

        private async Task<string> UserName(ulong userId)
        {
            var user = (IUser)_client.GetUser(userId) ?? await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? userId.ToString();
        }
    }
}
