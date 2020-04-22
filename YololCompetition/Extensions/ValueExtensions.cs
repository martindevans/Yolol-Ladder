using System;
using Yolol.Execution;

namespace YololCompetition.Extensions
{
    public static class ValueExtensions
    {
        public static string ToHumanString(this Value value)
        {
            if (value.Type == Yolol.Execution.Type.Number)
                return value.Number.ToString();
            else if (value.Type == Yolol.Execution.Type.String)
                return $"\"{value.String}\"";
            else
                throw new FormatException($"Cannot format value of type `{value.Type}`");
        }
    }
}
