using System;
using System.Linq;
using System.Threading.Tasks;
using BalderHash.Extensions;
using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Extensions;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Messages;
using MessageType = YololCompetition.Services.Messages.MessageType;

namespace YololCompetition.Modules
{
    [UsedImplicitly]
    public class Competition
        : BaseModule
    {
        private readonly IChallenges _challenges;
        private readonly IMessages _messages;

        public Competition(IChallenges challenges, IMessages messages)
        {
            _challenges = challenges;
            _messages = messages;
        }

        [Command("check-pool"), Summary("Check how may challenges there are ready to play")]
        public async Task CheckPool()
        {
            var count = await _challenges.GetPendingCount();
            var msg = await ReplyAsync($"There are {count} challenges pending");

            var emoji = count switch {
                0 => "😰", // cold sweat
                //1 => "😟", // worried
                //2 => "🙂", // smile
                //3 => "😁", // grin
                > 4 => "😲", // astonished
                _ => null,
            };

            if (emoji != null)
                await msg.AddReactionAsync(new Emoji(emoji));

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
                await _messages.TrackMessage(message, current.Id, MessageType.Current);

                Console.WriteLine($"Tracking message cid:{message.Channel.Id} mid:{message.Id}");
            }
        }

        [Command("competition"), Alias("challenge"), Summary("Search previous competitions")]
        public async Task GetCompetition(string search)
        {
            var results = await _challenges.FuzzyFindChallenge(search).ToListAsync();
            if (results.Count == 1)
                await ReplyAsync(embed: results[0].ToEmbed().Build());
            else
            {
                await DisplayItemList(
                    results,
                    () => "No challenges",
                    (c, i) => $"`[{((uint)c.Id).BalderHash()}]` {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
                );
            }
        }

        [Command("competitions"), Alias("challenges"), Summary("List all previous competitions")]
        public async Task ListCompetitions()
        {
            await DisplayItemList(
                await _challenges.GetChallenges().ToListAsync(),
                () => "No challenges",
                (c, i) => $"`[{((uint)c.Id).BalderHash()}]` {c.Name}" + (c.EndTime.HasValue && c.EndTime > DateTime.UtcNow ? " (Current)" : "")
            );
        }
    }
}
