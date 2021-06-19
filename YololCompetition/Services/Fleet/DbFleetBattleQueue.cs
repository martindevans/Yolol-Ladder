using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Fleet
{
    public class DbFleetBattleQueue
        : IFleetBattleQueue
    {
        private readonly IDatabase _db;
        private readonly IFleetStorage _fleets;
        private readonly IFleetRankings _ranks;

        public DbFleetBattleQueue(IDatabase db, IFleetStorage fleets, IFleetRankings ranks)
        {
            _db = db;
            _fleets = fleets;
            _ranks = ranks;

            try
            {
                _db.Exec(
                    "CREATE TABLE IF NOT EXISTS 'FleetsBattleQueue' (" +
                    "'FleetId1' INTEGER NOT NULL,"+
                    "'FleetId2' INTEGER NOT NULL,"+
                    "PRIMARY KEY('FleetId1','FleetId2'));"
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task Enqueue(Fleet fleet)
        {
            // Schedule a fight against all of the top fleets
            foreach (var item in await _ranks.GetTopTanks(64))
                await Enqueue(item.Fleet, fleet);

            // Schedule another fight against the very best fleets, this will
            // solidify the ranking of this new fleet if it's a contender for a top spot
            foreach (var item in await _ranks.GetTopTanks(4))
                await Enqueue(item.Fleet, fleet);
        }

        private async Task Enqueue(Fleet a, Fleet b)
        {
            if (a.Id == b.Id)
                return;

            await using var cmd = _db.CreateCommand();

            cmd.CommandText = "REPLACE INTO FleetsBattleQueue(FleetId1, FleetId2) Values(@A, @B);";

            cmd.Parameters.Add(new SqliteParameter("@A", DbType.UInt64) { Value = a.Id });
            cmd.Parameters.Add(new SqliteParameter("@B", DbType.UInt64) { Value = b.Id });

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<Battle>> Queue(uint limit = uint.MaxValue)
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _db.CreateCommand();

                cmd.CommandText = "SELECT * FROM FleetsBattleQueue LIMIT @Limit";

                cmd.Parameters.Add(new SqliteParameter("@Limit", DbType.UInt32) { Value = limit });

                return cmd;
            }

            var enumerable = new SqlAsyncResult<Battle>(_db, PrepareQuery, Battle.Parse);
            return await enumerable.ToListAsync();
        }

        public async Task Remove(Battle battle)
        {
            await using var cmd = _db.CreateCommand();

            cmd.CommandText = "DELETE FROM FleetsBattleQueue WHERE FleetId1 = @FleetId1 AND FleetId2 = @FleetId2";

            cmd.Parameters.Add(new SqliteParameter("@FleetId1", DbType.UInt64) {Value = battle.A });
            cmd.Parameters.Add(new SqliteParameter("@FleetId2", DbType.UInt64) {Value = battle.B });

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Battle?> GetNextBattle()
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT * FROM FleetsBattleQueue ORDER BY ROWID LIMIT 1;";
                return cmd;
            }

            var enumerable = new SqlAsyncResult<Battle?>(_db, PrepareQuery, a => Battle.Parse(a));
            return await enumerable.SingleOrDefaultAsync();
        }
    }
}
