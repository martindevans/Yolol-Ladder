using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Yolol.Execution;
using Yolol.IL.Extensions;
using YololCompetition.Attributes;
using YololCompetition.Extensions;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Verification;
using Type = Yolol.Execution.Type;

namespace YololCompetition.Modules
{
    public class Utility
        : ModuleBase
    {
        private readonly IYololExecutor _executor;

        public Utility(IYololExecutor executor)
        {
            _executor = executor;
        }

        [Command("chars"), Summary("Count the letters in a string")]
        public async Task CharCount([Remainder] string message)
        {
            await ReplyAsync($"{message.Trim('`').Replace("\n", "").Replace("\r", "").Length} characters");
        }

        [Command("check"), Summary("Attempt to parse some Yolol code, report syntax errors")]
        public async Task TryParse([Remainder] string input)
        {
            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var result = Yolol.Grammar.Parser.ParseProgram(code);
            if (!result.IsOk)
            {
                await ReplyAsync(result.Err.ToString());
                return;
            }

            await ReplyAsync($"Successfully parsed program! ```{result.Ok}```");
        }

        [Command("yolol"), Summary("Run some Yolol code. The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYolol([Remainder] string input)
        {
            // Try to get code from message
            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }
            
            // Try to parse code as Yolol
            var result = Yolol.Grammar.Parser.ParseProgram(code);
            if (!result.IsOk)
            {
                await ReplyAsync(result.Err.ToString());
                return;
            }

            // Prep execution state
            var compileTimer = new Stopwatch();
            compileTimer.Start();
            var state = _executor.Prepare(result.Ok);
            compileTimer.Stop();

            // Run for 2000 lines, 500ms or until `:done!=0`
            var exeTimer = new Stopwatch();
            exeTimer.Start();
            var err = await state.Run(2000, TimeSpan.FromMilliseconds(500));

            // Print out error if execution terminated for some reason
            if (err != null)
            {
                await ReplyAsync(err);
                return;
            }

            // Print out the final machine state
            var embed = state.ToEmbed(embed => {
                embed.AddField("Setup", $"{compileTimer.ElapsedMilliseconds}ms", true);
                embed.AddField("Execute", $"{exeTimer.ElapsedMilliseconds}ms", true);
            });
            await ReplyAsync(embed: embed.Build());
        }

        [Command("yolol-il"), Summary("Run some Yolol code using the new (much faster) Yolol.IL engine. The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololIL([Remainder] string input)
        {
            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var result = Yolol.Grammar.Parser.ParseProgram(code);
            if (!result.IsOk)
            {
                await ReplyAsync(result.Err.ToString());
                return;
            }

            // Compile program
            var compileTimer = new Stopwatch();
            compileTimer.Start();
            var internalsMap = new Dictionary<string, int>();
            var externalsMap = new Dictionary<string, int>();
            var lines = new List<Func<ArraySegment<Value>, ArraySegment<Value>, int>>();
            for (var i = 0; i < result.Ok.Lines.Count; i++)
            {
                lines.Add(result.Ok.Lines[i].Compile(
                    i + 1,
                    Math.Max(20, result.Ok.Lines.Count),
                    internalsMap,
                    externalsMap
                ));
            }
            compileTimer.Stop();

            // Setup state
            var internals = new Value[internalsMap.Count];
            Array.Fill(internals, new Value(0));
            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, new Value(0));

            var timer = new Stopwatch();
            timer.Start();

            // Run lines until completion indicator is set or execution time limit is exceeded
            var limit = 0;
            var pc = 0;
            while (++limit < 2000)
            {
                // Break out if `:done` is set
                if (externalsMap.TryGetValue(":done", out var doneIdx))
                    if (externals[doneIdx].ToBool())
                        break;

                try
                {
                    pc = lines[pc](internals, externals) - 1;
                }
                catch (ExecutionException)
                {
                    pc++;
                }

                // loop around if program counter goes over max
                if (pc >= 20)
                    pc = 0;

                // Execution timeout
                if (timer.ElapsedMilliseconds > 500)
                {
                    await ReplyAsync("Execution Timed Out!");
                    return;
                }

                // Sanity check strings are not getting too long
                var strings = (from v in internals
                               where v.Type == Type.String
                               select v)
                       .Concat(from v in externals
                               where v.Type == Type.String
                               select v);

                foreach (var str in strings)
                {
                    if (str.String.Length < 5000)
                        continue;
                    await ReplyAsync("Max String Length Exceeded!");
                    return;
                }
            }

            // Print out the final machine state
            var embed = new EmbedBuilder {
                Title = "Execution Finished (IL)",
                Color = Color.DarkPurple,
                Footer = new EmbedFooterBuilder().WithText("A Cylon Project")
            };

            embed.Description += $"{limit} lines executed. Next line: {pc + 1}";

            embed.AddField("Compile", $"{compileTimer.ElapsedMilliseconds}ms", true);
            embed.AddField("Execute", $"{timer.ElapsedMilliseconds}ms", true);

            if (internalsMap.Count > 0)
            {
                var locals = string.Join("\n", internalsMap.Select(kv => $"`{kv.Key}={internals[kv.Value].ToHumanString()}`"));
                embed.AddField("Locals", locals);
            }

            if (externalsMap.Count > 0)
            {
                var globals = string.Join("\n", externalsMap.Select(kv => $"`{kv.Key}={externals[kv.Value].ToHumanString()}`"));
                embed.AddField("Globals", globals);
            }

            await ReplyAsync(embed: embed.Build());
        }
    }
}
