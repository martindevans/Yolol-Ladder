using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Fleet
{
    public class DbFleetStorage
        : IFleetStorage
    {
        private readonly IDatabase _db;

        public DbFleetStorage(IDatabase db)
        {
            _db = db;

            try
            {
                _db.Exec(
                    "CREATE TABLE IF NOT EXISTS 'Fleets' (" +
                    "'FleetId' INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE," +
                    "'UserId' INTEGER NOT NULL," +
                    "'Name' TEXT NOT NULL," +
                    "'Blob' BLOB NOT NULL);"
                );

                _db.Exec(
                    "CREATE UNIQUE INDEX IF NOT EXISTS 'UniqueFleets' ON 'Fleets' (" +
                    "'UserId' ASC," +
                    "'Name' ASC);"
                );

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task<Fleet> Store(ulong userId, string name, byte[] bytes)
        {
            await using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "UPDATE Fleets Set BLOB=@DataBlob WHERE UserId=@UserId AND Name=@FleetName;" +
                                  "SELECT FleetId, UserId, Name FROM Fleets WHERE UserId=@UserId AND Name=@FleetName";

                cmd.Parameters.Add(new SqliteParameter("@UserId", DbType.UInt64) {Value = userId});
                cmd.Parameters.Add(new SqliteParameter("@FleetName", DbType.String) {Value = name});
                cmd.Parameters.Add(new SqliteParameter("@DataBlob", DbType.Binary) {Value = bytes});

                // If the any rows were returned then we just updated an already existing fleet
                await using var updated = await cmd.ExecuteReaderAsync();

                if (updated.HasRows)
                {
                    await updated.ReadAsync();
                    return Fleet.Read(updated);
                }
            }

            await using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Fleets(userId, Name, Blob) Values(@UserId, @FleetName, @DataBlob);" +
                                  "SELECT FleetId, UserId, Name FROM Fleets WHERE UserId=@UserId AND Name=@FleetName";

                cmd.Parameters.Add(new SqliteParameter("@UserId", DbType.UInt64) {Value = userId});
                cmd.Parameters.Add(new SqliteParameter("@FleetName", DbType.String) {Value = name});
                cmd.Parameters.Add(new SqliteParameter("@DataBlob", DbType.Binary) {Value = bytes});

                await using var inserted = await cmd.ExecuteReaderAsync();
                if (!inserted.HasRows)
                    throw new InvalidOperationException("Failed to insert fleet into database");

                await inserted.ReadAsync();
                return Fleet.Read(inserted);
            }
        }

        public async Task<Fleet?> Load(ulong id)
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT FleetId, UserId, Name FROM Fleets WHERE FleetId = @FleetId;";
                cmd.Parameters.Add(new SqliteParameter("@FleetId", DbType.UInt64) {Value = id});
                return cmd;
            }

            var enumerable = new SqlAsyncResult<Fleet?>(_db, PrepareQuery, a => Fleet.Read(a));
            return await enumerable.SingleOrDefaultAsync();
        }

        public async Task<byte[]?> LoadBlob(ulong id)
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT Blob FROM Fleets WHERE FleetId = @FleetId;";
                cmd.Parameters.Add(new SqliteParameter("@FleetId", DbType.UInt64) {Value = id});
                return cmd;
            }

            var enumerable = new SqlAsyncResult<byte[]?>(_db, PrepareQuery, a => (byte[])a["Blob"]);
            return await enumerable.SingleOrDefaultAsync();
        }
    }
}
