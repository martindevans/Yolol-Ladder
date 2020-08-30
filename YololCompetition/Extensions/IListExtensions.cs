using System;
using System.Collections.Generic;

namespace YololCompetition.Extensions
{
    public static class IListExtensions
    {
        public static T? RemoveFirst<T>(this IList<T> list, Func<T, bool> predicate)
            where T : struct
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    list.RemoveAt(i);
                    return list[i];
                }
            }

            return null;
        }

        public static bool ReplaceFirst<T>(this IList<T> list, Func<T, bool> predicate, T replacement)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    list[i] = replacement;
                    return true;
                }
            }

            return false;
        }

        public static bool ReplaceFirst<T>(this IList<T> list, Func<T, bool> predicate, Func<T, T> replace)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    list[i] = replace(list[i]);
                    return true;
                }
            }

            return false;
        }
    }
}
