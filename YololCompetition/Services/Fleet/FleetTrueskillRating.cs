using System.Data.Common;
using YololCompetition.Services.Trueskill;

namespace YololCompetition.Services.Fleet
{
    public readonly struct FleetTrueskillRating
    {
        public Fleet Fleet { get; }
        public TrueskillRating Rating { get; }

        public FleetTrueskillRating(Fleet fleet, TrueskillRating rating)
        {
            Fleet = fleet;
            Rating = rating;
        }

        public static FleetTrueskillRating Read(DbDataReader reader)
        {
            return new FleetTrueskillRating(
                Fleet.Read(reader),
                new TrueskillRating(
                    double.Parse(reader["Mean"].ToString()!),
                    double.Parse(reader["StdDev"].ToString()!)
                )
            );
        }
    }
}
