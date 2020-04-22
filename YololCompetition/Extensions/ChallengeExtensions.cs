using System;
using System.Linq;
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
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            if (challenge.EndTime.HasValue)
                embed.AddField("End Date", (challenge.EndTime.Value - DateTime.UtcNow).Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second));

            var inputs = challenge.Inputs.SelectMany(a => a.Keys).Distinct().Select(a => $"`:{a}`").Humanize("&");
            var outputs = challenge.Outputs.SelectMany(a => a.Keys).Distinct().Select(a => $"`:{a}`").Humanize("&");
            embed.Description = $"{challenge.Description}.\n" +
                                $"This challenge will present inputs in fields {inputs}. The output must be written into {outputs}. Set `:{challenge.CheckIndicator} = 1` to move to the next test case.";

            var examples = from item in challenge.Inputs.Zip(challenge.Outputs)
                           let i = string.Join(" ", item.First.Select(a => $":{a.Key}={a.Value.ToHumanString()}"))
                           let o = string.Join(" ", item.Second.Select(a => $":{a.Key}={a.Value.ToHumanString()}"))
                           select $"Inputs: `{i}`, Outputs:`{o}`";
            embed.AddField("**Examples**", "There are hundreds of test cases which your program must produce. Here are some examples:\n" + string.Join("\n", examples.Take(5)));

            return embed;
        }
    }
}
