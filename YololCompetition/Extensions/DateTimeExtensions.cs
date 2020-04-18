using System;

namespace YololCompetition.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);

        public static ulong UnixTimestamp(this DateTime time)
        {
            return (ulong)time.Subtract(UnixEpoch).TotalSeconds;
        }

        public static DateTime FromUnixTimestamp(this ulong unixTime)
        {
            var t = UnixEpoch.Add(TimeSpan.FromSeconds(unixTime));
            return new DateTime(t.Ticks, DateTimeKind.Utc);
        }
    }
}
