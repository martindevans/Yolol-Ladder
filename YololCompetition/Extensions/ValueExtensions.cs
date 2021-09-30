using System;
using Yolol.Execution;

namespace YololCompetition.Extensions
{
    public static class ValueExtensions
    {
        private const int StrLengthLimit = 256;

        public static string ToHumanString(this Value value)
        {
            if (value.Type == Yolol.Execution.Type.Number)
                return value.Number.ToString();
            else if (value.Type == Yolol.Execution.Type.String)
            {
                if (value.String.Length > StrLengthLimit)
                {
                    var trimmed = value.String.Length - StrLengthLimit;
                    return value.String.ToString().Substring(0, StrLengthLimit) + $" ...({trimmed} more characters trimmed)";
                }
                else
                    return $"\"{value.String}\"";
            }
            else
                throw new FormatException($"Cannot format value of type `{value.Type}`");
        }
    }
}
