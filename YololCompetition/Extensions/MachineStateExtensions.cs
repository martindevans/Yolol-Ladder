using System.Collections.Generic;
using Discord;
using System.Linq;
using Yolol.Execution;

namespace YololCompetition.Extensions
{
    public static class MachineStateExtensions
    {
        public static EmbedBuilder ToEmbed<TD>(this MachineState state, TD? network, int? iters, int pc)
            where TD : class, IDeviceNetwork, IEnumerable<(string, Value)>
        {
            var embed = new EmbedBuilder {
                Title = "Execution Finished",
                Color = Color.Purple,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            if (iters.HasValue)
                embed.Description += $"{iters} lines executed. ";
            embed.Description += $"Next line: {pc}";

            if (state.Any())
            {
                var locals = string.Join("\n", state.Select(a => $"`{a.Key}={a.Value.Value.ToHumanString()}`"));
                embed.AddField("Locals", locals);
            }

            if (network != null && network.Any())
            {
                var globals = string.Join("\n", network.Select(a => $"`:{a.Item1}={a.Item2.ToHumanString()}`"));
                embed.AddField("Globals", globals);
            }

            return embed;
        }
    }
}
