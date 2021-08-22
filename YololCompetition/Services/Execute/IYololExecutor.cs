using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Execute
{
    public interface IYololExecutor
    {
        IExecutionState Prepare(Yolol.Grammar.AST.Program program, string done = ":done");
    }

    public interface IExecutionState
        : IEnumerable<KeyValuePair<VariableName, Value>>
    {
        /// <summary>
        /// Get/Set the done variable value
        /// </summary>
        public bool Done { get; set; }

        /// <summary>
        /// Get the PC at the end of the latest execution
        /// </summary>
        public int ProgramCounter { get; }

        /// <summary>
        /// Get total line executed count so far
        /// </summary>
        public ulong TotalLinesExecuted { get; }

        /// <summary>
        /// Get/Set if overflowing (running off line 20) terminates execution
        /// </summary>
        public bool TerminateOnPcOverflow { get; set; }

        /// <summary>
        /// Execute the program for a maximum amount of lines, time or until `:done` is non-zero
        /// </summary>
        /// <param name="lineExecutionLimit">Max lines to run</param>
        /// <param name="timeout">Max time to execute for</param>
        /// <returns>The error which ended execution, or else null</returns>
        public string? Run(uint lineExecutionLimit, TimeSpan timeout);

        /// <summary>
        /// Try to get the value of a given variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Value? TryGet(VariableName name);

        /// <summary>
        /// Set a specific variable to a given value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void Set(VariableName name, Value value);

        /// <summary>
        /// Copy all the variable values from this state to another state
        /// </summary>
        /// <param name="other"></param>
        /// <param name="externalsOnly"></param>
        void CopyTo(IExecutionState other, bool externalsOnly = false);
    }

    public static class IExecutionStateExtensions
    {
        public static EmbedBuilder ToEmbed(this IExecutionState state, Action<EmbedBuilder>? pre = null)
        {
            var embed = new EmbedBuilder {
                Title = "Execution Finished",
                Color = Color.Purple,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            embed.Description += $"{state.TotalLinesExecuted} lines executed. Next line: {state.ProgramCounter}";

            pre?.Invoke(embed);

            var locals = state.Where(a => !a.Key.IsExternal).ToArray();
            if (locals.Length > 0)
                embed.AddField("Locals", string.Join("\n", locals.OrderBy(a => a.Key.Name).Select(a => $"`{a.Key}={a.Value.ToHumanString()}`")));

            var globals = state.Where(a => a.Key.IsExternal).ToArray();
            if (globals.Length > 0)
                embed.AddField("Globals", string.Join("\n", globals.OrderBy(a => a.Key.Name).Select(a => $"`{a.Key}={a.Value.ToHumanString()}`")));

            return embed;
        }
    }
}
