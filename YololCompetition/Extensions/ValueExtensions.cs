using System;
using Yolol.Execution;
using Type = Yolol.Execution.Type;

namespace YololCompetition.Extensions
{
    public static class ValueExtensions
    {
        private const int StrLengthLimit = 256;

        public static string ToHumanString(this Value value)
        {
            return value switch
            {
                { Type: Type.Number } => value.Number.ToString(),
                { Type: Type.String, String: { Length: <= StrLengthLimit } } => $"\"{value.String}\"",
                { Type: Type.String } => TrimLongString(value.String.ToString()),
                _ => throw new FormatException($"Cannot format value of type `{value.Type}`"),
            };

            static string TrimLongString(string value)
            {
                var left = value[..32];
                var right = value[^32..];
                var count = left.Length + right.Length;

                return $"\"{left}...({value.Length - count} characters omitted)...{right}\"";
            }
        }
    }
}
