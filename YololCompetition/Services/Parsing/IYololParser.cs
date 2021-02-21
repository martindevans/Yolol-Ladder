using System.Threading.Tasks;

namespace YololCompetition.Services.Parsing
{
    public interface IYololParser
    {
        Task<(Yolol.Grammar.AST.Program?, string?)> Parse(string input);
    }
}
