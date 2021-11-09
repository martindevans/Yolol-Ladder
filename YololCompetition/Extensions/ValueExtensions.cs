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
                { Type: Type.String } => $"\"{value.ToString()[..StrLengthLimit]}\" ...({value.String.Length - StrLengthLimit} more characters trimmed)",
                _ => throw new FormatException($"Cannot format value of type `{value.Type}`"),
            };
        }
    }
}
