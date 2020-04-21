using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;

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
            var match = Regex.Match(input, ".*?```(?<code>[^```]*?)```.*");

            if (!match.Success || match.Groups.Count == 0)
            {
                await ReplyAsync(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                return;
            }

            var code = match.Groups["code"].Value;

            var tokens = Yolol.Grammar.Tokenizer.TryTokenize(code);
            if (!tokens.HasValue)
            {
                await ReplyAsync($"Tokenizer Error: {tokens.FormatErrorMessageFragment()}");
                return;
            }

            var ast = Yolol.Grammar.Parser.TryParseProgram(tokens.Value);
            if (!ast.HasValue)
            {
                await ReplyAsync($"Parse Error: {ast.FormatErrorMessageFragment()}");
                return;
            }

            await ReplyAsync($"Successfully parsed program! ```{ast}```");
        }
    }
}
