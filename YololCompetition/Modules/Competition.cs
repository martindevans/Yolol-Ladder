using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using YololCompetition.Extensions;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Cron;

namespace YololCompetition.Modules
{
    public class Competition
        : ModuleBase
    {
        private readonly IChallenges _challenges;
        private readonly ICron _cron;

        public Competition(IChallenges challenges, ICron cron)
        {
            _challenges = challenges;
            _cron = cron;
        }

        [Command("current"), Summary("Show the current competition details")]
        public async Task CurrentCompetition()
        {
            var current = await _challenges.GetCurrentChallenge();
            if (current == null)
                await ReplyAsync("There is no challenge currently running");
            else
            {
                var message = await ReplyAsync(embed: current.ToEmbed().Build());

                _cron.Schedule(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), uint.MaxValue, async () => {

                    // Get current challenge
                    var c = await _challenges.GetCurrentChallenge();

                    // Update embed
                    await message.ModifyAsync(a => a.Embed = current.ToEmbed().Build());

                    // Keep running this task while the challenge is the same challenge that was initially scheduled
                    return c?.Id == current.Id;
                });
            }
        }

        [Command("competition"), Summary("Search previous competitions")]
        public async Task GetCompetition(string search)
        {
            // Try parsing the string as a challenge ID
            if (ulong.TryParse(search, out var uid))
            {
                var c = await _challenges.GetChallenges(id: uid).SingleOrDefaultAsync();
                if (c != null)
                {
                    await ReplyAsync(embed: c.ToEmbed().Build());
                    return;
                }
            }

            // Try searching for a challenge that matches the name
            var matches = await _challenges.GetChallenges(name: search).ToArrayAsync();
            if (matches.Length == 1)
            {
                await ReplyAsync(embed: matches[0].ToEmbed().Build());
            }
            else
            {
                await DisplayItemList(
                    matches.ToAsyncEnumerable(),
                    () => "No challenges",
                    (c, i) => $"[{c.Id}] {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
                );
            }
        }

        [Command("competitions"), Summary("List all previous competitions")]
        public async Task ListCompetitions()
        {
            await DisplayItemList(
                _challenges.GetChallenges(null),
                () => "No challenges",
                (c, i) => $"[{c.Id}] {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
            );
        }

        private async Task DisplayItemList<T>(IAsyncEnumerable<T> items, Func<string> nothing, Func<T, int, string> itemToString)
        {
            var builder = new StringBuilder();

            var none = true;
            var index = 0;
            await foreach (var item in items)
            {
                none = false;    

                var str = itemToString(item, index++);
                if (builder.Length + str.Length > 1000)
                {
                    await ReplyAsync(builder.ToString());
                    builder.Clear();
                }

                builder.Append(str);
                builder.Append('\n');
            }

            if (builder.Length > 0)
                await ReplyAsync(builder.ToString());

            if (none)
                await ReplyAsync(nothing());
        }
    }
}
