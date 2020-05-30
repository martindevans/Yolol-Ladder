using System;
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

        public static uint Levenshtein(this string? a, string? b)
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return (uint)b!.Length;
            else if (b == null)
                return (uint)a!.Length;

            if (a == null || b == null)
                return 0;

            var aLength = (uint)a.Length;
            var bLength = (uint)b.Length;
            var matrix = new int[aLength + 1, bLength + 1];

            for (var i = 0; i <= aLength;)
                matrix[i, 0] = i++;
            for (var j = 0; j <= bLength;)
                matrix[0, j] = j++;

            for (var i = 1; i <= aLength; i++)
            {
                for (var j = 1; j <= bLength; j++)
                {
                    var cost = b[j - 1] == a[i - 1] ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return (uint)matrix[aLength, bLength];
        }
    }
}
