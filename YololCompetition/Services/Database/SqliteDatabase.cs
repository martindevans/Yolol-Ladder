using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace YololCompetition.Services.Database
{
    public class SqliteDatabase
        : IDatabase
    {
        private readonly SqliteConnection _dbConnection;

        public SqliteDatabase(Configuration config)
        {
            SQLitePCL.raw.sqlite3_config(2); // SQLITE_CONFIG_MULTITHREAD

            _dbConnection = new SqliteConnection(config.DatabaseConnectionString);
            _dbConnection.Open();
        }

        public DbCommand CreateCommand()
        {
            return _dbConnection.CreateCommand();
        }
    }

    // ReSharper disable once InconsistentNaming
    public static class IDatabaseServiceExtensions
    {
        public static int Exec(this IDatabase db, string sql)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        public static async Task<int> ExecAsync(this IDatabase db, string sql)
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return await cmd.ExecuteNonQueryAsync();
        }
    }
}