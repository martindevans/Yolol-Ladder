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
using BalderHash.Extensions;
using Discord;
using Discord.Addons.Interactive;
using YololCompetition.Services.Solutions;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class CompetitionAdmin
        : InteractiveBase
    {
        private readonly IChallenges _challenges;
        private readonly IScheduler _scheduler;
        private readonly ISolutions _solutions;

        public CompetitionAdmin(IChallenges challenges, IScheduler scheduler, ISolutions solutions)
        {
            _challenges = challenges;
            _scheduler = scheduler;
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

            await ReplyAsync("Do you want to create this challenge (yes/no)?");
            var confirm = (await NextMessageAsync(timeout: TimeSpan.FromMilliseconds(-1))).Content;
            if (!confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync("Cancelled creating challenge!");
                return;
            }

            var c = new Challenge(0, title, "done", data.In, data.Out, null, difficulty, desc, data.Shuffle ?? true, data.Mode ?? ScoreMode.BasicScoring, data.Chip ?? YololChip.Professional);
            await _challenges.Create(c);
            await ReplyAsync("Challenge added to queue");
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
            var uid = BalderHash.BalderHash64.Parse(id);
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
                await ReplyAsync($" - {challenge.Name} (`{challenge.Id.BalderHash()}`)");
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
    }
}
