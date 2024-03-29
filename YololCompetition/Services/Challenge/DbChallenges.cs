﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Extensions;
using YololCompetition.Services.Database;

namespace YololCompetition.Services.Challenge
{
    public class DbChallenges
        : IChallenges
    {
        private readonly IDatabase _database;
        private readonly Configuration _config;

        public DbChallenges(IDatabase database, Configuration config)
        {
            _database = database;
            _config = config;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Challenges` (" +
                               "`Status` INTEGER NOT NULL, " +
                               "`Name` TEXT NOT NULL, " +
                               "`Inputs` TEXT NOT NULL, " +
                               "`Outputs` TEXT NOT NULL, " +
                               "`Difficulty` INTEGER NOT NULL, " +
                               "`CheckIndicator` TEXT NOT NULL, " +
                               "`Description` TEXT NOT NULL, " +
                               "`Shuffle` INTEGER NOT NULL, " +
                               "`ScoreMode` INTEGER NOT NULL, " +
                               "`ID` INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, " +
                               "`EndUnixTime` INTEGER, " +
                               "`IntermediateCode` TEXT NOT NULL, " +
                               "`Chip` INTEGER NOT NULL);");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task Create(Challenge challenge)
        {
            await using var cmd = _database.CreateCommand();

            cmd.CommandText = "INSERT into Challenges" +
                              "(Status, Name, Inputs, Outputs, CheckIndicator, Difficulty, Description, Shuffle, ScoreMode, Chip, IntermediateCode)" +
                              "values(@Status, @Name, @Inputs, @Outputs, @CheckIndicator, @Difficulty, @Description, @Shuffle, @ScoreMode, @Chip, @IntermediateCode)";
            challenge.Write(cmd.Parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Update(Challenge challenge)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "UPDATE Challenges SET Difficulty = @Difficulty, EndUnixTime = @EndUnixTime, IntermediateCode = @IntermediateCode WHERE ID = @ID";
            challenge.Write(cmd.Parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<long> GetPendingCount()
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "SELECT Count(*) FROM Challenges WHERE Status = 1";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        private IAsyncEnumerable<Challenge> GetPending(int limit)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM Challenges WHERE Status = 1 ORDER BY random() LIMIT @Limit";
                cmd.Parameters.Add(new SqliteParameter("@Limit", DbType.UInt32) { Value = limit });
                return cmd;
            }

            return new SqlAsyncResult<Challenge>(_database, PrepareQuery, Challenge.Read);
        }

        public async Task<Challenge?> StartNext()
        {
            var pending = await GetPending(1).FirstOrDefaultAsync();
            if (pending == null || !pending.Intermediate.IsOk)
                return null;

            // End time of the challenge should be Challenge Duration + start time of today to account for if Referee was down on the day it was supposed to start the challenge.
            var endTime = (DateTime.UtcNow.Date.AddMinutes(_config.ChallengeStartTime) + TimeSpan.FromHours(_config.ChallengeDurationHours)).UnixTimestamp();

            // Set status to "Running" and end time to an appropriate offset from now
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "UPDATE Challenges SET Status = 2, EndUnixTime = @EndUnixTime WHERE ID = @ID";
            cmd.Parameters.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = (int)pending.Id }); 
            cmd.Parameters.Add(new SqliteParameter("@EndUnixTime", DbType.UInt64) { Value = endTime }); 
            await cmd.ExecuteNonQueryAsync();

            return await GetCurrentChallenge();
        }

        public async Task<int> SetToPending(ulong challengeId)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "UPDATE Challenges SET Status = 1 WHERE ID = @ID";
            cmd.Parameters.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = challengeId }); 

            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task EndCurrentChallenge(bool terminate)
        {
            var status = terminate ? ChallengeStatus.Terminated : ChallengeStatus.Complete;

            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "UPDATE Challenges SET Status = @NewStatus WHERE Status = 2";
            cmd.Parameters.Add(new SqliteParameter("@NewStatus", DbType.UInt64) { Value = (int)status });
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ChangeChallengeDifficulty(Challenge challenge, ChallengeDifficulty difficulty)
        {
            // Set status to "Running" and end time to an appropriate offset from now
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "UPDATE Challenges SET Difficulty = @Difficulty WHERE ID = @ID";
            cmd.Parameters.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = challenge.Id }); 
            cmd.Parameters.Add(new SqliteParameter("@Difficulty", DbType.UInt64) { Value = (ulong)difficulty }); 
            await cmd.ExecuteNonQueryAsync();
        }

        public IAsyncEnumerable<Challenge> GetChallenges(ChallengeDifficulty? difficultyFilter = null, ulong? id = null, string? name = null, bool includeUnstarted = false)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM Challenges " +
                                  "WHERE (Difficulty = @Difficulty or @Difficulty IS null) " +
                                  "AND (ID = @ID or @ID IS null) " +
                                  "AND (Name LIKE @Name or @Name IS null) " +
                                  "AND ((NOT EndUnixTime IS null) or @IncludeUnstarted) " +
                                  "ORDER BY EndUnixTime DESC ";

                cmd.Parameters.Add(new SqliteParameter("@Difficulty", DbType.UInt64) { Value = (object?)difficultyFilter ?? DBNull.Value });
                cmd.Parameters.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = (object?)id ?? DBNull.Value });
                cmd.Parameters.Add(new SqliteParameter("@Name", DbType.UInt64) { Value = (object?)name ?? DBNull.Value });
                cmd.Parameters.Add(new SqliteParameter("@IncludeUnstarted", DbType.Boolean) { Value = includeUnstarted });

                return cmd;
            }

            return new SqlAsyncResult<Challenge>(_database, PrepareQuery, Challenge.Read);
        }

        public async Task<int> Delete(ulong challengeId)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "DELETE FROM Challenges WHERE ID = @ID";
            cmd.Parameters.Add(new SqliteParameter("@ID", DbType.UInt64) { Value = challengeId });
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Challenge?> GetCurrentChallenge()
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "SELECT * FROM Challenges WHERE Status = 2 LIMIT 1";
            var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;
            return Challenge.Read(reader);
        }

        /// <summary>
        /// Get all challenges which will end after the given timestamp
        /// </summary>
        /// <param name="endUnixTime"></param>
        /// <returns></returns>
        public IAsyncEnumerable<Challenge> GetChallengesByEndTime(ulong endUnixTime)
        {
            DbCommand PrepareQuery(IDatabase db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM Challenges WHERE EndUnixTime >= @EndTime AND Status != 5 ORDER BY EndUnixTime";
                cmd.Parameters.Add(new SqliteParameter("@EndTime", DbType.UInt64) { Value = endUnixTime });
                return cmd;
            }

            return new SqlAsyncResult<Challenge>(_database, PrepareQuery, Challenge.Read);
        }
    }
}
