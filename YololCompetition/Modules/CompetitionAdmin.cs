using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using Yolol.Execution;
using YololCompetition.Serialization.Json;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Schedule;
using System.Linq;
using System.Text;
using BalderHash.Extensions;
using Discord;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using YololCompetition.Services.Parsing;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Verification;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class CompetitionAdmin
        : InteractiveBase
    {
        private readonly IChallenges _challenges;
        private readonly IScheduler _scheduler;
        private readonly IVerification _verification;
        private readonly DiscordSocketClient _client;
        private readonly IYololParser _parser;
        private readonly ISolutions _solutions;

        public CompetitionAdmin(IChallenges challenges, IScheduler scheduler, ISolutions solutions, IVerification verification, DiscordSocketClient client, IYololParser parser)
        {
            _challenges = challenges;
            _scheduler = scheduler;
            _verification = verification;
            _client = client;
            _parser = parser;
            _solutions = solutions;
        }

        [Command("create"), Summary("Create a new challenge")]
        public async Task Create()
        {
            await ReplyAsync("What is the challenge title?");
            var title = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;

            await ReplyAsync("What is the challenge description?");
            var desc = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;

            var levels = string.Join(',', Enum.GetNames(typeof(ChallengeDifficulty)));
            await ReplyAsync($"What is the challenge difficulty ({levels})?");
            var difficulty = Enum.Parse<ChallengeDifficulty>((await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content);

            await ReplyAsync("What is the challenge URL (raw JSON)?");
            var url = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var result))
            {
                await ReplyAsync("Invalid URL format");
                return;
            }

            if (result.Host != "gist.githubusercontent.com")
            {
                await ReplyAsync("URL must begin with `gist.githubusercontent.com`");
                return;
            }

            Data? data;
            try
            {
                using var wc = new WebClient();
                var json = wc.DownloadString(result);
                data = JsonConvert.DeserializeObject<Data>(json, new JsonSerializerSettings {
                    Converters = new JsonConverter[] {
                        new YololValueConverter()
                    },
                    FloatParseHandling = FloatParseHandling.Decimal
                });
            }
            catch (Exception e)
            {
                await ReplyAsync("Failed: " + e.Message.Substring(0, Math.Min(1000, e.Message.Length)));
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

            var program = await Parse(data.Code ?? "");
            if (program == null)
            {
                await ReplyAsync("Invalid Program. Cancelling creation.");
                return;
            }

            await ReplyAsync("Do you want to create this challenge (yes/no)?");
            var confirm = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Cancelled creating challenge!");
                return;
            }

            var c = new Challenge(0, title, "done", data.In, data.Out, null, difficulty, desc, data.Shuffle ?? true, data.Mode ?? ScoreMode.BasicScoring, data.Chip ?? YololChip.Professional, program);
            await _challenges.Create(c);
            await ReplyAsync("Challenge added to queue");
        }

        private async Task<Yolol.Grammar.AST.Program?> Parse(string input)
        {
            // Try to parse code as Yolol
            var result = Yolol.Grammar.Parser.ParseProgram(input);
            if (!result.IsOk)
            {
                await ReplyAsync($"```{result.Err}```");
                return null;
            }

            return result.Ok;
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
                await ReplyAsync($" - {challenge.Name} (`{challenge.Id.BalderHash()}`)");
                await Task.Delay(10);
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
            var confirm = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;
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
            var confirm = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not rescoring anything");
                return;
            }

            var solutions = await _solutions.GetSolutions(c.Id, uint.MaxValue).ToArrayAsync();
            const string pbarHeader = "Rescoring: ";
            var progress = await ReplyAsync(pbarHeader);

            var totalTicks = 0l;
            var failures = 0;
            var results = new List<RescoreItem>(solutions.Length);
            for (var i = 0; i < solutions.Length; i++)
            {
                await progress.ModifyAsync(a => a.Content = $"{pbarHeader} {i}/{solutions.Length}");

                var s = solutions[i];
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

                await Task.Delay(100);
            }

            await progress.ModifyAsync(a => a.Content = $"Completed rescoring ({totalTicks} total ticks)");

            if (failures > 0)
                await ReplyAsync($"{failures} solutions failed to verify");

            var report = new StringBuilder();
            report.AppendLine($"Rescoring `{c.Name}`");
            foreach (var rescore in results)
                report.AppendLine($"{await UserName(rescore.Before.UserId)}: {rescore.Before.Score} => {rescore.After.Score}");
            await ReplyAsync(report.ToString());

            await ReplyAsync("Apply rescoring to this challenge (yes/no)?");
            var confirm2 = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;
            if (!confirm2.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Not applying rescoring");
                return;
            }

            foreach (var rescore in results)
                await _solutions.SetSolution(rescore.After);

            await ReplyAsync("Done.");
        }

        private readonly struct RescoreItem
        {
            public readonly Solution Before;
            public readonly Solution After;
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
