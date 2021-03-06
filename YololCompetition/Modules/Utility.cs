﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Attributes;
using YololCompetition.Extensions;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Parsing;

namespace YololCompetition.Modules
{
    [UsedImplicitly]
    public class Utility
        : ModuleBase
    {
        private readonly IYololExecutor _executor;
        private readonly IYololParser _parser;

        public Utility(IYololExecutor executor, IYololParser parser)
        {
            _executor = executor;
            _parser = parser;
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

        [Command("yolol"), Summary("Run some Yolol code. The program will run for 100000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYolol([Remainder] string input)
        {
            await RunYolol(input, _executor, 100000);
        }

        [Command("yolol-il"), Hidden, Summary("Run some Yolol code (using the IL engine). The program will run for 100000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololIL([Remainder] string input)
        {
            await RunYolol(input, new YololCompileExecutor(), 100000);
        }

        [Command("yolol-legacy"), Hidden, Summary("Run some Yolol code (using the legacy interpreter). The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololEmu([Remainder] string input)
        {
            await RunYolol(input, new YololInterpretExecutor(), 2000);
        }

        private async Task<Yolol.Grammar.AST.Program?> Parse(string input)
        {
            var (program, error) = await _parser.Parse(input);

            if (program != null)
                return program;

            await ReplyAsync(error);
            return null;
        }

        private async Task RunYolol(string input, IYololExecutor executor, uint lines)
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

            // Run for N lines, 500ms or until `:done!=0`
            var exeTimer = new Stopwatch();
            exeTimer.Start();
            var err = state.Run(lines, TimeSpan.FromMilliseconds(500));

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
