using F23.StringSimilarity;
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

        public static string LimitLength(this string str, int max)
        {
            return str.Length < max
                 ? str
                 : str[..max];
        }

        private static readonly Levenshtein _levenshtein = new();
        public static uint Levenshtein(this string str, string other)
        {
            return (uint)_levenshtein.Distance(str, other);
        }
    }
}
