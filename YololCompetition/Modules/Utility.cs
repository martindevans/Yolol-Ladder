using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using Yolol.Analysis.ControlFlowGraph;
using Yolol.Analysis.ControlFlowGraph.Extensions;
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
        private readonly string? _yogi;
        private readonly string? _debuggerUrl;

        public Utility(IYololExecutor executor, IYololParser parser, Configuration config)
        {
            _executor = executor;
            _parser = parser;
            _yogi = config.YogiPath;
            _debuggerUrl = config.OnlineDebuggerUrl;
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

        [Command("yolol"), Summary("Run some Yolol code. The program will run for 1000000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYolol([Remainder] string input)
        {
            await RunYolol(input, _executor, 1000000);
        }

        [Command("yolol-il"), Hidden, Summary("Run some Yolol code (using the IL engine). The program will run for 1000000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololIL([Remainder] string input)
        {
            await RunYolol(input, new YololCompileExecutor(), 1000000);
        }

        [Command("yolol-legacy"), Hidden, Summary("Run some Yolol code (using the legacy interpreter). The program will run for 2000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 1, "Please wait a short while before running more code")]
        public async Task RunYololEmu([Remainder] string input)
        {
            await RunYolol(input, new YololInterpretExecutor(), 2000);
        }

        [Command("yolol-yogi"), Hidden, Summary("Run some Yolol code (using the yogi vm). The program will run for 20000 iterations or until `done` is set to a true value.")]
        [RateLimit("6F6429AE-BFF5-480C-953E-FE3A70726A01", 4, "Please wait a short while before running more code")]
        public async Task RunYololYogi([Remainder] string input)
        {
            if (_yogi == null || !File.Exists(_yogi))
            {
                await ReplyAsync("Cannot find `Yogi` executable (Contact `Martin#2468` or `rad dude broham#2970`)");
                return;
            }

            await RunYolol(input, new YogiExecutor(_yogi), 20000);
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
            var state = await executor.Prepare(result);
            compileTimer.Stop();

            // Run for N lines, 500ms or until `:done!=0`
            var exeTimer = new Stopwatch();
            exeTimer.Start();
            var saved = state.Serialize();
            var err = await state.Run(lines, TimeSpan.FromMilliseconds(500));

            // Print out the final machine state
            var embed = state.ToEmbed(embed => 
            {
                embed.AddField("Setup", $"{compileTimer.ElapsedMilliseconds}ms", true);
                embed.AddField("Execute", $"{exeTimer.ElapsedMilliseconds}ms", true);

                if (err != null)
                    embed.AddField("Error", $"{err}", false);

                if (_debuggerUrl != null)
                {
                    var base64 = saved.Serialize();
                    embed.WithUrl($"{_debuggerUrl}?state={base64}");
                }
            });
            await ReplyAsync(embed: embed.Build());
        }

        [Command("cfg"), Hidden, Summary("Print out the CFG of some Yolol code.")]
        public async Task PrintCfg([Remainder] string message)
        {
            var result = await Parse(message);
            if (result == null)
                return;

            var builder = new Builder(result, Math.Max(20, result.Lines.Count));
            var cfg = builder.Build();
            var dot = cfg.ToDot();

            await using var stream = new MemoryStream(dot.Length);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(dot);
            await writer.FlushAsync();
            stream.Position = 0;
            await Context.Channel.SendFileAsync(stream, "hint.txt", "Message is too long!");
        }
    }
}
