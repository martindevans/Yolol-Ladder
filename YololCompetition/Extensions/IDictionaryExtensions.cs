using System.Collections.Generic;

namespace YololCompetition.Extensions
{
    public static class IDictionaryExtensions
    {
        public static TV GetOrAdd<TK, TV>(this IDictionary<TK, TV> dictionary, TK key, TV defaultValue)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;

            dictionary.Add(key, defaultValue);
            return defaultValue;
        }
    }
}
