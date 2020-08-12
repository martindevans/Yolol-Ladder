using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Yolol.Execution;
using YololCompetition.Attributes;
using YololCompetition.Extensions;
using YololCompetition.Services.Verification;

namespace YololCompetition.Modules
{
    public class Utility
        : ModuleBase
    {
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

            var network = new DefaultValueDeviceNetwork();
            var state = new MachineState(network);
            var done = state.GetVariable(":done");

            var timer = new Stopwatch();
            timer.Start();

            // Run lines until completion indicator is set or execution time limit is exceeded
            var limit = 0;
            var pc = 0;
            while (!done.Value.ToBool() && limit++ < 2000)
            {
                try
                {
                    // If line if blank, just move to the next line
                    if (pc >= result.Ok.Lines.Count)
                        pc++;
                    else
                        pc = result.Ok.Lines[pc].Evaluate(pc, state);
                }
                catch (ExecutionException)
                {
                    pc++;
                }

                // loop around if program counter goes over max
                if (pc >= 20)
                    pc = 0;

                // Occasionally delay the task a little to make sure it can't dominate other work
                if (limit % 100 == 10)
                    await Task.Delay(1);

                // Execution timeout
                if (timer.ElapsedMilliseconds > 500)
                {
                    await ReplyAsync("Execution Timed Out!");
                    return;
                }

                // Sanity check strings are not getting too long
                var strings = (from v in state
                               where v.Value.Value.Type == Type.String
                               select v.Value.Value)
                       .Concat(from v in network
                               where v.Item2.Type == Type.String
                               select v.Item2);

                foreach (var str in strings)
                {
                    if (str.String.Length < 5000)
                        continue;
                    await ReplyAsync("Max String Length Exceeded!");
                    return;
                }
            }

            // Print out the final machine state
            var embed = state.ToEmbed(network, limit, pc + 1).Build();
            await ReplyAsync(embed: embed);
        }
    }
}
