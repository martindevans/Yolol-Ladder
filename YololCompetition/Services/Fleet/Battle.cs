using System.Data.Common;

namespace YololCompetition.Services.Fleet
{
    public readonly struct Battle
    {
        public ulong A { get; }
        public ulong B { get; }

        public Battle(ulong a, ulong b)
        {
            A = a;
            B = b;
        }

        public static Battle Parse(DbDataReader reader)
        {
            return new Battle(
                ulong.Parse(reader["FleetId1"].ToString()!),
                ulong.Parse(reader["FleetId2"].ToString()!)
            );
        }
    }
}
