using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;
using Yolol.Execution;
using YololCompetition.Serialization.Json;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Cron;
using YololCompetition.Services.Schedule;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class CompetitionAdmin
        : ModuleBase
    {
        private readonly IChallenges _challenges;
        private readonly IScheduler _scheduler;

        public CompetitionAdmin(IChallenges challenges, IScheduler scheduler, ICron cron)
        {
            _challenges = challenges;
            _scheduler = scheduler;
        }

        [Command("create"), Summary("Create a new challenge")]
        public async Task Create(string title, string description, ChallengeDifficulty difficulty, string url)
        {
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

            var c = new Challenge(0, title, "done", data.In, data.Out, null, difficulty, description, data.Shuffle ?? true, ScoreMode.BasicScoring);
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
        }


        [Command("check-pool"), Summary("Check state of challenge pool")]
        public async Task CheckPool()
        {
            var count = await _challenges.GetPendingCount();
            await ReplyAsync($"There are {count} challenges pending");
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
    }
}
