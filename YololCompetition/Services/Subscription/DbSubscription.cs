using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Subscription
{
    public class DbSubscription
        : ISubscription
    {
        private readonly IDatabase _database;

        public DbSubscription(IDatabase database)
        {
            _database = database;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Subscriptions` (`Channel` INTEGER NOT NULL, `Guild` INTEGER NOT NULL, PRIMARY KEY(`Channel`, `Guild`))");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task Subscribe(ulong channel, ulong guild)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT into Subscriptions (Channel, Guild) values(@Channel, @Guild)";
            cmd.Parameters.Add(new SqliteParameter("@Channel", DbType.UInt64) { Value = channel });
            cmd.Parameters.Add(new SqliteParameter("@Guild", DbType.UInt64) { Value = guild });
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Unsubscribe(ulong channel, ulong guild)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "DELETE FROM Subscriptions WHERE Channel = @Channel AND Guild = @Guild";
            cmd.Parameters.Add(new SqliteParameter("@Channel", DbType.UInt64) { Value = channel });
            cmd.Parameters.Add(new SqliteParameter("@Guild", DbType.UInt64) { Value = guild });
            await cmd.ExecuteNonQueryAsync();
        }

        public IAsyncEnumerable<Subscription> GetSubscriptions()
        {
            static DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM Subscriptions ORDER BY Guild";
                return cmd;
            }

            static Subscription ParseSubscription(DbDataReader reader)
            {
                return new Subscription(
                    ulong.Parse(reader["Channel"].ToString()!),
                    ulong.Parse(reader["Guild"].ToString()!)
                );
            }

            return new SqlAsyncResult<Subscription>(_database, PrepareQuery, ParseSubscription);
        }
    }
}
