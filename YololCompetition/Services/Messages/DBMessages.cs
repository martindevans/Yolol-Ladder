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

        public async Task TrackMessage(ulong channelID, ulong messageID, ulong challengeID, MessageType messageType)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT into Messages (ChannelID, MessageID, ChallengeID, MessageType) values(@ChannelID, @MessageID, @ChallengeID, @MessageType)";
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = channelID });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = messageID });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeID", DbType.UInt64) { Value = challengeID });
            cmd.Parameters.Add(new SqliteParameter("@MessageType", DbType.UInt64) { Value = messageType });
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
                cmd.CommandText = "SELECT * from Messages";
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
                if (!(_client.GetChannel(message.ChannelID) is ISocketMessageChannel channel))
                {
                    Console.WriteLine($"No such channel: {message.ChannelID}");
                    await RemoveMessage(message);
                    return;
                }

                if (!((await channel.GetMessageAsync(message.MessageID)) is IUserMessage msg))
                {
                    Console.WriteLine($"No such message: {message.MessageID}");
                    await RemoveMessage(message);
                    return;
                }

                await msg.ModifyAsync(a => a.Embed = challenge.ToEmbed().Build());
                if (currentChallenge == null || challenge.Id != currentChallenge.Id)
                    await RemoveMessage(message);
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
            _cron.Schedule(TimeSpan.FromSeconds(30), default, async ct => {

                await foreach (var entry in GetMessages().WithCancellation(ct))
                {
                    try
                    {
                        switch (entry.MessageType) {
                            case MessageType.Current:
                                await UpdateCurrentMessage(entry);
                                break;

                            case MessageType.Leaderboard:
                                throw new NotImplementedException("MessageType.Leaderboard");

                            default:
                                Console.WriteLine($"Invalid Message type {entry.MessageType} for Message {entry.MessageID} from channel {entry.ChannelID} For challenge {entry.ChallengeID}");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                var current = await _challenges.GetCurrentChallenge();
                if (current == null)
                    return TimeSpan.FromMinutes(5);

                // This shouldn't ever happen - the current challenge should always have an end time!
                if (current.EndTime == null)
                    return TimeSpan.FromMinutes(1);

                var duration = current.EndTime.Value - DateTime.UtcNow;
                if (duration > TimeSpan.FromMinutes(5))
                    return TimeSpan.FromMinutes(5);

                return duration;

            });
        }
    }
}
