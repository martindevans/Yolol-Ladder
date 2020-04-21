using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Leaderboard
{
    public class DbLeaderboard
        : ILeaderboard
    {
        private readonly IDatabase _database;

        public DbLeaderboard(IDatabase database)
        {
            _database = database;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Leaderboard` (`UserId` TEXT NOT NULL UNIQUE, `Score` INTEGER NOT NULL, PRIMARY KEY(`UserId`));");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public IAsyncEnumerable<RankInfo> GetUserNearRanks(ulong userId, byte above, byte below)
        {
            DbCommand PrepareQueryBetter(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Leaderboard " +
                                  "WHERE (Score >= (SELECT Score FROM Leaderboard WHERE UserId = @UserId)) " +
                                  "ORDER BY Rank LIMIT @Limit";
                cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@Limit", System.Data.DbType.Int32) { Value = (int)above });
                cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@UserId", System.Data.DbType.String) { Value = userId.ToString() });
                return cmd;
            }

            DbCommand PrepareQueryWorse(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Leaderboard " +
                                  "WHERE (Score < (SELECT Score FROM Leaderboard WHERE UserId = @UserId)) " +
                                  "ORDER BY Rank LIMIT @Limit";
                cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@Limit", System.Data.DbType.Int32) { Value = (int)below });
                cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@UserId", System.Data.DbType.String) { Value = userId.ToString() });
                return cmd;
            }

            var better = new SqlAsyncResult<RankInfo>(_database, PrepareQueryBetter, ParseRankInfo);
            var worse = new SqlAsyncResult<RankInfo>(_database, PrepareQueryWorse, ParseRankInfo);

            return from item in better.Concat(worse)
                   orderby item.Rank
                   select item;
        }

        public IAsyncEnumerable<RankInfo> GetTopRank(byte count)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Leaderboard ORDER BY Rank LIMIT @Limit;";
                cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@Limit", System.Data.DbType.Int32) { Value = (int)count });
                return cmd;
            }

            return new SqlAsyncResult<RankInfo>(_database, PrepareQuery, ParseRankInfo);
        }

        public async Task AddScore(ulong userId, uint score)
        {
            await using var cmd = _database.CreateCommand();

            cmd.CommandText = "INSERT INTO Leaderboard(UserId, Score) values(@UserId, @Score) ON CONFLICT(UserId) DO UPDATE SET Score = Score + @Score";
            cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@UserId", System.Data.DbType.String) { Value = userId.ToString() });
            cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@Score", System.Data.DbType.Int64) { Value = (long)score });
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SubtractScore(ulong userId, uint score)
        {
            await using var cmd = _database.CreateCommand();

            cmd.CommandText = "INSERT INTO Leaderboard(UserId, Score) values(@UserId, 0) ON CONFLICT(UserId) DO UPDATE SET Score = Max(0, Score - @Score)";
            cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@UserId", System.Data.DbType.String) { Value = userId.ToString() });
            cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@Score", System.Data.DbType.Int64) { Value = (long)score });
            await cmd.ExecuteNonQueryAsync();
        }

        private static RankInfo ParseRankInfo(DbDataReader reader)
        {
            return new RankInfo(
                ulong.Parse(reader["UserId"].ToString()!),
                uint.Parse(reader["Rank"].ToString()!),
                uint.Parse(reader["Score"].ToString()!)
            );
        }
    }
}
