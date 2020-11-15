using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;
using System.Linq;

namespace YololCompetition.Services.Trueskill
{
    public class DbTrueskill
        : ITrueskill
    {
        private readonly IDatabase _database;

        public DbTrueskill(IDatabase database)
        {
            _database = database;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Trueskill` (`UserId` TEXT NOT NULL UNIQUE, `Mean` NUMERIC NOT NULL, `StdDev` NUMERIC NOT NULL, `Grace` INTEGER NOT NULL, PRIMARY KEY(`UserId`));");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static TrueskillRating ParseRating(DbDataReader reader)
        {
            return new TrueskillRating(
                ulong.Parse(reader["UserId"].ToString()!),
                uint.Parse(reader["Rank"].ToString()!),
                double.Parse(reader["Mean"].ToString()!),
                double.Parse(reader["StdDev"].ToString()!)
            );
        }

        public async Task<TrueskillRating?> GetRating(ulong userId)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM (SELECT DENSE_RANK() OVER (ORDER BY -(Mean - 3 * StdDev)) Rank, * FROM Trueskill) WHERE UserId = @UserId;";
                cmd.Parameters.Add(new SqliteParameter("@UserId", System.Data.DbType.UInt64) { Value = userId });
                return cmd;
            }

            return await new SqlAsyncResult<TrueskillRating>(_database, PrepareQuery, ParseRating).AsAsyncEnumerable().Select(a => (TrueskillRating?)a).SingleOrDefaultAsync();
        }

        public IAsyncEnumerable<TrueskillRating> GetTopRanks(int count)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM (SELECT DENSE_RANK() OVER (ORDER BY -(Mean - 3 * StdDev)) Rank, * FROM Trueskill) ORDER BY Rank LIMIT @Limit;";
                cmd.Parameters.Add(new SqliteParameter("@Limit", System.Data.DbType.Int32) { Value = count });
                return cmd;
            }

            return new SqlAsyncResult<TrueskillRating>(_database, PrepareQuery, ParseRating);
        }

        public async Task SetRating(ulong userId, double mean, double stdDev)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT INTO Trueskill(UserId, Mean, StdDev, Grace) values(@UserId, @Mean, @StdDev, 0) ON CONFLICT(UserId) DO UPDATE SET Mean = @Mean, StdDev = @StdDev, Grace = 0";
            cmd.Parameters.Add(new SqliteParameter("@UserId", System.Data.DbType.String) { Value = userId.ToString() });
            cmd.Parameters.Add(new SqliteParameter("@Mean", System.Data.DbType.Double) { Value = mean });
            cmd.Parameters.Add(new SqliteParameter("@StdDev", System.Data.DbType.Double) { Value = stdDev });
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Decay(int gracePeriod, double amount = 1.1, double threshold = 3.5)
        {
            // Increment grace for all rows
            await using var cmdInc = _database.CreateCommand();
            cmdInc.CommandText = "UPDATE Trueskill SET Grace = Grace + 1";
            await cmdInc.ExecuteNonQueryAsync();
            
            // Decay stddev for all rows with low grace and stddev below threshold
            await using var cmdDecay = _database.CreateCommand();
            cmdDecay.CommandText = "UPDATE Trueskill SET StdDev = min(StdDev * @DecayFactor, @Threshold) WHERE Grace > @GracePeriod AND StdDev < @Threshold";
            cmdDecay.Parameters.Add(new SqliteParameter("@DecayFactor", System.Data.DbType.Double) { Value = amount });
            cmdDecay.Parameters.Add(new SqliteParameter("@GracePeriod", System.Data.DbType.Int32) { Value = gracePeriod });
            cmdDecay.Parameters.Add(new SqliteParameter("@Threshold", System.Data.DbType.Double) { Value = threshold });
            await cmdDecay.ExecuteNonQueryAsync();
        }

        public async Task Clear()
        {
            await using var cmdInc = _database.CreateCommand();
            cmdInc.CommandText = "DELETE from Trueskill";
            await cmdInc.ExecuteNonQueryAsync();
        }
    }
}
