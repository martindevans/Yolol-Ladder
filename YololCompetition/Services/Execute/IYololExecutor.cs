using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Execute
{
    public static class IYololExecutorExtensions
    {
        public static async Task<IExecutionState> Prepare(this IYololExecutor executor, Yolol.Grammar.AST.Program program, string done = ":done")
        {
            return (await executor.Prepare(new[] { program }, done)).Single();
        }
    }

    public interface IYololExecutor
    {
        Task<IEnumerable<IExecutionState>> Prepare(IEnumerable<Yolol.Grammar.AST.Program> programs, string done = ":done");
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
        public Task<string?> Run(uint lineExecutionLimit, TimeSpan timeout);

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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
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

            BuildFieldsList(embed, "Locals", state.Where(a => !a.Key.IsExternal).ToArray());
            BuildFieldsList(embed, "Globals", state.Where(a => a.Key.IsExternal).ToArray());

            return embed;
        }

        private static void BuildFieldsList(EmbedBuilder embed, string title, IReadOnlyList<KeyValuePair<VariableName, Value>> values)
        {
            var counter = 0;
            var builder = new StringBuilder();
            foreach (var (name, value) in values)
            {
                var str = $"`{name}={value.ToHumanString()}`";
                if (builder.Length + str.Length > 1000)
                {
                    var c = counter++;
                    embed.AddField($"{title} {(c == 0 ? "" : c.ToString())}", builder.ToString(), false);
                    builder.Clear();
                }

                builder.AppendLine(str);
            }

            if (builder.Length > 0)
                embed.AddField($"{title} {(counter == 0 ? "" : counter.ToString())}", builder.ToString(), false);
        }

        public static void CopyTo(this IExecutionState from, IExecutionState to, bool externalsOnly = false)
        {
            foreach (var (name, value) in from)
            {
                if (!name.IsExternal && externalsOnly)
                    continue;
                to.Set(name, value);
            }
        }
    }
}
