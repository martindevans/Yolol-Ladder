using System.Text.RegularExpressions;

namespace YololCompetition.Extensions
{
    public static class StringExtensions
    {
        public static string? ExtractYololCodeBlock(this string str)
        {
            var match = Regex.Match(str, ".*?```(?<code>[^```]*?)```.*");

            if (!match.Success || match.Groups.Count == 0)
                return null;

            return match.Groups["code"].Value;
        }
    }
}
