using System.Threading.Tasks;
using Discord.Commands;
using Yolol.Execution;
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
            await ReplyAsync($"{message.Trim('`').Length} characters");
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

        [Command("yolol"), Summary("Run some Yolol code. The program will run for 500 iterations or until `done` is set to a true value.")]
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

            // Run lines until completion indicator is set or execution time limit is exceeded
            var limit = 0;
            var pc = 0;
            while (!done.Value.ToBool() && limit++ < 500)
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
            }

            // Print out the final machine state
            var embed = state.ToEmbed(network, limit - 1, pc + 1).Build();
            await ReplyAsync(embed: embed);
        }
    }
}
