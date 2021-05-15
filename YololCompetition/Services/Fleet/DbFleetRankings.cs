using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Moserware.Skills;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Fleet
{
    public class DbFleetRankings
        : IFleetRankings
    {
        private readonly IDatabase _db;
        private readonly GameInfo _gameinfo;

        public DbFleetRankings(IDatabase db, GameInfo gameinfo)
        {
            _db = db;
            _gameinfo = gameinfo;

            try
            {
                _db.Exec(
                    "CREATE TABLE IF NOT EXISTS 'FleetsRanks' (" +
                    "'FleetId' INTEGER NOT NULL," +
                    "'Mean' NUMERIC NOT NULL," +
                    "'StdDev' INTEGER NOT NULL," +
                    "PRIMARY KEY('FleetId'));"
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task ResetRank(Fleet fleet)
        {
            await SetRank(fleet.Id, _gameinfo.DefaultRating.Mean, _gameinfo.DefaultRating.StandardDeviation);
        }

        public async Task<IReadOnlyList<FleetTrueskillRating>> GetTopTanks(uint limit)
        {
            DbCommand PrepareQuery(IDatabase database)
            {
                var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT Fleets.FleetId, Mean, StdDev, UserId, Name FROM FleetsRanks INNER JOIN Fleets ON Fleets.FleetId=FleetsRanks.FleetId ORDER BY (Mean - 3 * StdDev) DESC";
                cmd.Parameters.Add(new SqliteParameter("@Limit", DbType.UInt32) { Value = limit });
                return cmd;
            }

            var enumerable = new SqlAsyncResult<FleetTrueskillRating>(_db, PrepareQuery, FleetTrueskillRating.Read);
            return await enumerable.ToListAsync();
        }

        public async Task<FleetTrueskillRating?> GetRank(ulong id)
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT Fleets.FleetId, Mean, StdDev, UserId, Name FROM FleetsRanks INNER JOIN Fleets ON Fleets.FleetId=FleetsRanks.FleetId WHERE Fleets.FleetId = @FleetId;";
                cmd.Parameters.Add(new SqliteParameter("@FleetId", DbType.UInt64) {Value = id});
                return cmd;
            }

            var enumerable = new SqlAsyncResult<FleetTrueskillRating?>(_db, PrepareQuery, a => FleetTrueskillRating.Read(a));
            return await enumerable.SingleOrDefaultAsync();
        }

        public async Task SetRank(ulong id, double mean, double stddev)
        {
            await using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM FleetsRanks WHERE FleetId=@FleetId; INSERT INTO FleetsRanks Values(@FleetId, @Mean, @StdDev);";

                cmd.Parameters.Add(new SqliteParameter("@FleetId", DbType.UInt64) { Value = id });
                cmd.Parameters.Add(new SqliteParameter("@Mean", DbType.String) { Value = mean });
                cmd.Parameters.Add(new SqliteParameter("@StdDev", DbType.Binary) { Value = stddev });

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task Update(ulong winner, ulong loser, bool draw)
        {
            var winnerRank = (await GetRank(winner))?.Rating ?? new Trueskill.TrueskillRating(_gameinfo.DefaultRating.Mean, _gameinfo.DefaultRating.StandardDeviation);
            var loserRank = (await GetRank(loser))?.Rating ?? new Trueskill.TrueskillRating(_gameinfo.DefaultRating.Mean, _gameinfo.DefaultRating.StandardDeviation);

            var teams = new[] {
                new Dictionary<ulong, Rating> { { winner, new Rating(winnerRank.Mean, winnerRank.StdDev) } },
                new Dictionary<ulong, Rating> { { loser, new Rating(loserRank.Mean, loserRank.StdDev) } },
            };

            var results = TrueSkillCalculator.CalculateNewRatings(
                _gameinfo,
                teams,
                draw ? new[] { 0, 0 } : new[] { 0, 1 }
            );

            var winnerResult = results[winner];
            var loserResult = results[loser];

            await SetRank(winner, winnerResult.Mean, winnerResult.StandardDeviation);
            await SetRank(loser, loserResult.Mean, loserResult.StandardDeviation);
        }
    }
}
