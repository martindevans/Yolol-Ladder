using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using YololCompetition.Services.Database;
using System.Linq;
using Microsoft.Data.Sqlite;

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

        public IAsyncEnumerable<RankInfo> GetTopRank(int count)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Leaderboard ORDER BY Rank LIMIT @Limit;";
                cmd.Parameters.Add(new SqliteParameter("@Limit", System.Data.DbType.Int32) { Value = (int)count });
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

        public async Task<RankInfo?> GetRank(ulong userId)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM (SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Leaderboard) WHERE UserId = @UserId";
                cmd.Parameters.Add(new SqliteParameter("@UserId", System.Data.DbType.UInt64) { Value = userId });
                return cmd;
            }

            return await new SqlAsyncResult<RankInfo>(_database, PrepareQuery, ParseRankInfo).Select(a => (RankInfo?)a).SingleOrDefaultAsync();
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
