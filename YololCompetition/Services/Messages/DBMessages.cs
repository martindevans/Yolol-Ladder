using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YololCompetition.Services.Database;
using YololCompetition.Services.Cron;
using YololCompetition.Services.Challenge;
using YololCompetition.Extensions;
using Discord.WebSocket;
using Discord;

namespace YololCompetition.Services.Messages
{
    public class DbMessages
        : IMessages
    {
        private readonly IDatabase _database;
        private readonly ICron _cron;
        private readonly IChallenges _challenges;
        private readonly BaseSocketClient _client;

        public enum messageType : uint {Current, Leaderboard}
        
        public DbMessages(IDatabase database, ICron cron, IChallenges challenges, BaseSocketClient client)
        {
            _database = database;
            _cron = cron;
            _challenges = challenges;
            _client = client;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Messages` (`ChannelID` INTEGER NOT NULL, `MessageID` INTEGER NOT NULL, `ChallengeID` INTEGER NOT NULL, 'MessageType' INTEGER NOT NULL, PRIMARY KEY(`MessageID`, `ChallengeID'))");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task TrackMessage(ulong ChannelID, ulong MessageID, ulong ChallengeID, uint MessageType)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT into Messages (ChannelID, MessageID, ChallengeID, MessageType) values(@ChannelID, @MessageID, @ChallengeID, @MessageType)";
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = ChannelID });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = MessageID });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeID", DbType.UInt64) { Value = ChallengeID });
            cmd.Parameters.Add(new SqliteParameter("@MessageType", DbType.UInt64) { Value = MessageType });
            await cmd.ExecuteNonQueryAsync();
        }

        public IAsyncEnumerable<Message> GetCurrentMessages(ulong challengeID)
        {
            DbCommand PrepareQuery(IDatabase database)
            {
                var cmd = _database.CreateCommand();
                cmd.CommandText = "SELECT from 'Messages' WHERE 'ChallengID' = @challengeID";
                cmd.Parameters.Add(new SqliteParameter("@ChallengeID", DbType.UInt64) { Value = challengeID});
                return cmd;
            }

            return new SqlAsyncResult<Message>(_database, PrepareQuery, ParseMessage);
        }

        private static Message ParseMessage(DbDataReader reader)
        {
            return new Message(
                ulong.Parse(reader["ChannelID"].ToString()!),
                ulong.Parse(reader["MessageID"].ToString()!),
                ulong.Parse(reader["ChallengeID"].ToString()!),
                uint.Parse(reader["MessageType"].ToString()!)
            );
        }

        public void StartMessageWatch()
        {
            _cron.Schedule(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), uint.MaxValue, async () => {
                // Get current challenge
                var c = await _challenges.GetCurrentChallenge();

                var messages = GetCurrentMessages(c.Id);
                await foreach (Message entry in messages) 
                {
                    
                    if (entry.ChallengeID != c.Id) {continue;}

                    switch (entry.MessageType) {
                        default:
                        case 0:
                        var channel = _client.GetChannel(entry.ChannelID);
                        var chn = channel as ISocketMessageChannel;
                        IUserMessage msg = chn.GetMessageAsync(entry.MessageID) as IUserMessage; 

                        await msg.ModifyAsync(a => a.Embed = c.ToEmbed().Build());
                        break;
                    }
                }

                return true;      
            });

        }
    }
}
