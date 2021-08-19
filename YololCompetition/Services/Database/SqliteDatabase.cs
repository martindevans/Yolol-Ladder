using System;
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
            _dbConnection = new SqliteConnection(config.DatabaseConnectionString);
            _dbConnection.Open();

            try
            {
                using var cmd = _dbConnection.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
    }
}