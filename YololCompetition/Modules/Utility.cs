﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        [Command("ast"), Summary("Print of the Abstract Syntax Tree of some code")]
        public async Task PrintAst([Remainder] string message)
        {
            var result = await Parse(message);
            if (result == null)
                return;

            var output = result.PrintAst();
            await ReplyAsync(output);
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
                await ReplyAsync($"```{result.Err}```");
            else
                await ReplyAsync($"Successfully parsed program! ```{result.Ok}```");
        }

        [Command("yolol"), Summary("Run some Yolol code. The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYolol([Remainder] string input)
        {
            await RunYolol(input, _executor);
        }

        [Command("yolol-il"), Hidden, Summary("Run some Yolol code (using the new experimental IL engine). The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololIL([Remainder] string input)
        {
            await RunYolol(input, new YololCompileExecutor());
        }

        [Command("yolol-legacy"), Hidden, Summary("Run some Yolol code (using the legacy interpreter). The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololEmu([Remainder] string input)
        {
            await RunYolol(input, new YololInterpretExecutor());
        }

        private async Task<Yolol.Grammar.AST.Program?> Parse(string input)
        {
            // Try to get code from message
            var code = input.ExtractYololCodeBlock();
            if (code == null)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return null;
            }
            
            // Try to parse code as Yolol
            var result = Yolol.Grammar.Parser.ParseProgram(code);
            if (!result.IsOk)
            {
                await ReplyAsync($"```{result.Err}```");
                return null;
            }
            else
                return result.Ok;
        }

        private async Task RunYolol(string input, IYololExecutor executor)
        {
            // Try to parse code as Yolol
            var result = await Parse(input);
            if (result == null)
                return;

            // Prep execution state
            var compileTimer = new Stopwatch();
            compileTimer.Start();
            var state = executor.Prepare(result);
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
    }
}
