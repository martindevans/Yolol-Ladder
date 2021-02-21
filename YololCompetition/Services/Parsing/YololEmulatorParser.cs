using System.Threading.Tasks;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Parsing
{
    public class YololEmulatorParser
        : IYololParser
    {
        public async Task<(Yolol.Grammar.AST.Program?, string?)> Parse(string input)
        {
            // Try to get code from message
            var code = input.ExtractYololCodeBlock();
            if (code == null)
                return (null, @"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");

            // Try to parse code as Yolol
            var result = Yolol.Grammar.Parser.ParseProgram(code);
            if (!result.IsOk)
                return (null, $"```{result.Err}```");
            
            return (result.Ok, null);
        }
    }
}
