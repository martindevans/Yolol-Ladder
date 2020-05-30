using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BalderHash.Extensions;
using Discord.Commands;
using YololCompetition.Extensions;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Messages;

namespace YololCompetition.Modules
{
    public class Competition
        : ModuleBase
    {
        private readonly IChallenges _challenges;
        private readonly IMessages _messages;

        public Competition(IChallenges challenges, IMessages messages)
        {
            _challenges = challenges;
            _messages = messages;
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
                await _messages.TrackMessage(message.Channel.Id, message.Id, current.Id, 0);
            }
        }

        [Command("competition"), Summary("Search previous competitions")]
        public async Task GetCompetition(string search)
        {
            var results = await _challenges.FuzzyFindChallenge(search).ToArrayAsync();
            if (results.Length == 1)
                await ReplyAsync(embed: results[0].ToEmbed().Build());
            else
            {
                await DisplayItemList(
                    results.ToAsyncEnumerable(),
                    () => "No challenges",
                    (c, i) => $"`[{((uint)c.Id).BalderHash()}]` {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
                );
            }
        }

        [Command("competitions"), Summary("List all previous competitions")]
        public async Task ListCompetitions()
        {
            await DisplayItemList(
                _challenges.GetChallenges(),
                () => "No challenges",
                (c, i) => $"`[{((uint)c.Id).BalderHash()}]` {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
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
