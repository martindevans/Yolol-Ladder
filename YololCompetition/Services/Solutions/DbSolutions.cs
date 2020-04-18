using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Solutions
{
    public class DbSolutions
        : ISolutions
    {
        private readonly IDatabase _database;

        public DbSolutions(IDatabase database)
        {
            _database = database;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Solutions` (`ChallengeId` INTEGER NOT NULL,`UserId` INTEGER NOT NULL,`Score` INTEGER NOT NULL,`Yolol` TEXT NOT NULL,PRIMARY KEY(`ChallengeId`,`UserId`));");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task SetSolution(Solution solution)
        {
            await using var cmd = _database.CreateCommand();

            cmd.CommandText = "INSERT INTO Solutions Values(@ChallengeId, @UserId, @Score, @Yolol) ON CONFLICT(UserId, ChallengeId) DO UPDATE SET Score = @Score, Yolol = @Yolol WHERE ChallengeId = @ChallengeId AND UserId = @UserId AND Score <= @Score";

            cmd.Parameters.Add(new SqliteParameter("@UserId", DbType.UInt64) { Value = solution.UserId });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeId", DbType.UInt64) { Value = solution.ChallengeId });
            cmd.Parameters.Add(new SqliteParameter("@Score", DbType.UInt32) { Value = solution.Score });
            cmd.Parameters.Add(new SqliteParameter("@Yolol", DbType.String) { Value = solution.Yolol });

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Solution?> GetSolution(ulong userId, ulong challengeId)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM Solutions WHERE UserId = @UserId AND ChallengeId = @ChallengeId";
                cmd.Parameters.Add(new SqliteParameter("@UserId", DbType.UInt64) { Value = userId });
                cmd.Parameters.Add(new SqliteParameter("@ChallengeId", DbType.UInt64) { Value = challengeId });
                return cmd;
            }

            return await new SqlAsyncResult<Solution>(_database, PrepareQuery, ParseSolution)
                  .Select(a => (Solution?)a)
                  .FirstOrDefaultAsync();
        }

        public IAsyncEnumerable<RankedSolution> GetSolutions(ulong challengeId, uint limit, uint minScore = uint.MinValue, uint maxScore = uint.MaxValue)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Solutions WHERE ChallengeId = @ChallengeId AND Score >= @MinScore AND Score <= @MaxScore ORDER BY -Score LIMIT @Limit";
                cmd.Parameters.Add(new SqliteParameter("@ChallengeId", DbType.UInt64) { Value = challengeId });
                cmd.Parameters.Add(new SqliteParameter("@Limit", DbType.UInt32) { Value = limit });
                cmd.Parameters.Add(new SqliteParameter("@MinScore", DbType.UInt32) { Value = minScore });
                cmd.Parameters.Add(new SqliteParameter("@MaxScore", DbType.UInt32) { Value = maxScore });
                return cmd;
            }

            return new SqlAsyncResult<RankedSolution>(_database, PrepareQuery, ParseRanked);
        }

        public async Task<RankedSolution?> GetRank(ulong challengeId, ulong userId)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "SELECT * FROM (SELECT DENSE_RANK() OVER (ORDER BY -Score) Rank, * FROM Solutions WHERE ChallengeId = @ChallengeId) WHERE UserId = @UserId";
            cmd.Parameters.Add(new SqliteParameter("@UserId", DbType.UInt64) { Value = userId });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeId", DbType.UInt64) { Value = challengeId });

            var results = await cmd.ExecuteReaderAsync();

            if (!results.HasRows)
                return null;

            await results.ReadAsync();
            return ParseRanked(results);
        }

        private static RankedSolution ParseRanked(DbDataReader reader)
        {
            return new RankedSolution(
                ParseSolution(reader),
                uint.Parse(reader["Rank"].ToString()!)
            );
        }

        private static Solution ParseSolution(DbDataReader reader)
        {
            return new Solution(
                ulong.Parse(reader["ChallengeId"].ToString()!),
                ulong.Parse(reader["UserId"].ToString()!),
                uint.Parse(reader["Score"].ToString()!),
                reader["Yolol"].ToString()!
            );
        }
    }
}
