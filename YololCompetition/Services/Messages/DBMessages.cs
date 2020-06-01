using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        private readonly DiscordSocketClient _client;
        
        public DbMessages(IDatabase database, ICron cron, IChallenges challenges, DiscordSocketClient client)
        {
            _database = database;
            _cron = cron;
            _challenges = challenges;
            _client = client;

            try
            {
                _database.Exec("CREATE TABLE IF NOT EXISTS `Messages` (`ChannelID` INTEGER NOT NULL, `MessageID` INTEGER NOT NULL, `ChallengeID` INTEGER NOT NULL, `MessageType` INTEGER NOT NULL, PRIMARY KEY(`MessageID`, `ChallengeID`))");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task TrackMessage(ulong ChannelID, ulong MessageID, ulong ChallengeID, MessageType MessageType)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT into Messages (ChannelID, MessageID, ChallengeID, MessageType) values(@ChannelID, @MessageID, @ChallengeID, @MessageType)";
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = ChannelID });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = MessageID });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeID", DbType.UInt64) { Value = ChallengeID });
            cmd.Parameters.Add(new SqliteParameter("@MessageType", DbType.UInt64) { Value = MessageType });
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RemoveMessage(Message message)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "DELETE FROM Messages WHERE ChannelID = @ChannelID AND MessageID = @MessageID" ;
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = message.ChannelID });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = message.MessageID });
            await cmd.ExecuteNonQueryAsync();
        }

        public IAsyncEnumerable<Message> GetMessages()
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _database.CreateCommand();
                cmd.CommandText = "SELECT from `Messages`";
                return cmd;
            }
            return new SqlAsyncResult<Message>(_database, PrepareQuery, ParseMessage);            
        }

        public async Task UpdateCurrentMessage(Message message)
        {
            var currentChallenge = await _challenges.GetCurrentChallenge();        
            var challenge = await _challenges.GetChallenges(id: message.ChallengeID).FirstAsync();

            if (challenge == null)
            {
                Console.WriteLine("Message exists for inexistant challenge " + message.ChallengeID);
                await RemoveMessage(message);
            }
            else
            {
                var channel = _client.GetChannel(message.ChannelID) as ISocketMessageChannel;                        
                var msg = channel?.GetMessageAsync(message.MessageID) as IUserMessage;

                if (msg != null)
                {
                await msg.ModifyAsync(a => a.Embed = challenge.ToEmbed().Build());
                }
                else
                {
                    Console.WriteLine ("Message " + message.MessageID + " from channel " + message.ChannelID + " is null");
                }

                if (currentChallenge == null || challenge != currentChallenge)
                {
                    await RemoveMessage(message);
                }
            }            
        }

        private static Message ParseMessage(DbDataReader reader)
        {
            return new Message(
                ulong.Parse(reader["ChannelID"].ToString()!),
                ulong.Parse(reader["MessageID"].ToString()!),
                ulong.Parse(reader["ChallengeID"].ToString()!),
                (MessageType)uint.Parse(reader["MessageType"].ToString()!)
            );
        }

        public void StartMessageWatch()
        {
            _cron.Schedule(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), uint.MaxValue, async () => {
                try 
                {
                    var messages = GetMessages();
                    await foreach (Message entry in messages) 
                    {
                        try
                        {
                            switch (entry.MessageType) {
                                case 0:
                                    await UpdateCurrentMessage(entry);
                                    break;
                                default:
                                    Console.WriteLine("Invalid Message type " + entry.MessageType + " for Message " + entry.MessageID + " from channel " + entry.ChannelID + " For challenge " + entry.ChallengeID);
                                    break;
                            }
                        }
                        catch (Exception E)
                        {
                            Console.WriteLine(E);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                return true;      
            });
        }
    }
}
