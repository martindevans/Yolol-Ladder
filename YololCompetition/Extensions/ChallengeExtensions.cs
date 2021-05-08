using System;
using System.Linq;
using System.Text;
using BalderHash.Extensions;
using Discord;
using Humanizer;
using YololCompetition.Services.Challenge;

namespace YololCompetition.Extensions
{
    public static class ChallengeExtensions
    {
        public static EmbedBuilder ToEmbed(this Challenge challenge)
        {
            var embed = new EmbedBuilder {
                Title = $"{challenge.Name} ({challenge.Difficulty})",
                Color = Color.Green,
                Footer = new EmbedFooterBuilder().WithText($"{((uint)challenge.Id).BalderHash()} - A Cylon Project ({DateTime.UtcNow.Ticks.GetHashCode()})")
            };

            if (challenge.EndTime.HasValue)
            {
                if (challenge.EndTime.Value < DateTime.UtcNow)
                    embed.AddField("End Time", "Completed");
                else
                    embed.AddField("End Time", (challenge.EndTime.Value - DateTime.UtcNow).Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second));
            }

            var inputs = challenge.Inputs.SelectMany(a => a.Keys).Distinct().Select(a => $"`:{a}`").Humanize("&");
            var outputs = challenge.Outputs.SelectMany(a => a.Keys).Distinct().Select(a => $"`:{a}`").Humanize("&");
            embed.Description = $"{challenge.Description}.\n" +
                                $"This challenge will present inputs in fields {inputs}. The output must be written into {outputs}. Set `:{challenge.CheckIndicator} = 1` to move to the next test case.";

            if (challenge.Chip != YololChip.Professional)
                embed.Description += $" This challenge is limited to operations available on a **{challenge.Chip}** level Yolol chip.";

            var examples = (from item in challenge.Inputs.Zip(challenge.Outputs)
                            let i = string.Join(" ", item.First.Select(a => $":{a.Key}={a.Value.ToHumanString()}"))
                            let o = string.Join(" ", item.Second.Select(a => $":{a.Key}={a.Value.ToHumanString()}"))
                            select $"Inputs: `{i}`, Outputs: `{o}`\n").Take(5);

            var exampleBuilder = new StringBuilder("There are hundreds of test cases which your program must produce. Here are some examples:\n");
            foreach (var example in examples)
                if (exampleBuilder.Length + example.Length < 1000)
                    exampleBuilder.Append(example);

            embed.AddField("**Examples**", exampleBuilder.ToString());

            return embed;
        }
    }
}
