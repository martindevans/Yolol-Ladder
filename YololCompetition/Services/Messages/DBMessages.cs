using System;
using System.Collections.Concurrent;
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
using Discord.Net;

namespace YololCompetition.Services.Messages
{
    public class DbMessages
        : IMessages
    {
        private readonly IDatabase _database;
        private readonly ICron _cron;
        private readonly IChallenges _challenges;
        private readonly DiscordSocketClient _client;

        private readonly ConcurrentDictionary<(ulong, ulong), IUserMessage> _messageCache = new();
        
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

        public async Task TrackMessage(IUserMessage message, ulong challengeID, MessageType messageType)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "INSERT into Messages (ChannelID, MessageID, ChallengeID, MessageType) values(@ChannelID, @MessageID, @ChallengeID, @MessageType)";
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = message.Channel.Id });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = message.Id });
            cmd.Parameters.Add(new SqliteParameter("@ChallengeID", DbType.UInt64) { Value = challengeID });
            cmd.Parameters.Add(new SqliteParameter("@MessageType", DbType.UInt64) { Value = messageType });
            await cmd.ExecuteNonQueryAsync();

            // Add message to cache
            var key = (message.Channel.Id, message.Id);
            _messageCache[key] = message;
        }

        private async Task RemoveMessage(Message message)
        {
            await using var cmd = _database.CreateCommand();
            cmd.CommandText = "DELETE FROM Messages WHERE ChannelID = @ChannelID AND MessageID = @MessageID" ;
            cmd.Parameters.Add(new SqliteParameter("@ChannelID", DbType.UInt64) { Value = message.ChannelID });
            cmd.Parameters.Add(new SqliteParameter("@MessageID", DbType.UInt64) { Value = message.MessageID });
            await cmd.ExecuteNonQueryAsync();
        }

        private IAsyncEnumerable<Message> GetMessages()
        {
            DbCommand PrepareQuery(IDatabase database)
            {                
                var cmd = _database.CreateCommand();
                cmd.CommandText = "SELECT * from Messages";
                return cmd;
            }
            return new SqlAsyncResult<Message>(_database, PrepareQuery, Message.Parse);            
        }

        private async Task<IUserMessage?> GetMessage(Challenge.Challenge? currentChallenge, Message message)
        {
            // If the message is no longer relevant, remove it from the DB
            if (currentChallenge == null || message.ChallengeID != currentChallenge.Id)
                await RemoveMessage(message);

            // Try to get message from cache
            var key = (message.ChannelID, message.MessageID);
            if (_messageCache.TryGetValue(key, out var cached))
                return cached;

            // Try to get the channel from Discord
            if (_client.GetChannel(message.ChannelID) is not ISocketMessageChannel channel)
            {
                Console.WriteLine($"No such channel: {message.ChannelID}");
                await RemoveMessage(message);
                return null;
            }

            // Check if bot has permission to read channel
            var gc = channel as IGuildChannel;
            if (gc != null)
            {
                var u = await gc.Guild.GetCurrentUserAsync();
                var p = u.GetPermissions(gc);
                if (!p.ReadMessageHistory)
                    return null;
            }

            IUserMessage? msg;
            try
            {
                msg = await channel.GetMessageAsync(message.MessageID) as IUserMessage;
                if (msg == null)
                {
                    Console.WriteLine($"No such message: {message.MessageID}");
                    await RemoveMessage(message);
                    return null;
                }
            }
            catch (HttpException ex)
            {
                Console.WriteLine($"Failed to get message {message.MessageID} in channel {message.ChannelID}({channel.Name}/{gc?.Guild.Name}): {ex.Message}");
                return null;
            }

            // Add it to the cache
            _messageCache[key] = msg;
            return msg;
        }

        private async Task UpdateCurrentMessage(Challenge.Challenge? currentChallenge, Message message)
        {
            var msg = await GetMessage(currentChallenge, message);
            if (msg != null)
            {
                var challenge = await _challenges.GetChallenges(id: message.ChallengeID).FirstOrDefaultAsync();
                if (challenge == null)
                {
                    Console.WriteLine("Message exists for nonexistant challenge " + message.ChallengeID);
                    await RemoveMessage(message);
                }
                else
                {
                    await msg.ModifyAsync(a => a.Embed = challenge.ToEmbed().Build());
                }
            }
        }

        public void StartMessageWatch()
        {
            _cron.Schedule(TimeSpan.FromSeconds(120), default, async ct => {

                // Get the current challenge
                var current = await _challenges.GetCurrentChallenge();

                // Loop through all messages, attempting to update them
                await foreach (var entry in GetMessages().WithCancellation(ct))
                {
                    try
                    {
                        switch (entry.MessageType)
                        {
                            case MessageType.Current:
                                await UpdateCurrentMessage(current, entry);
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

                    // Add in some delay between each event to make sure we don't get near the discord rate limit (2 events per second)
                    await Task.Delay(1000, ct);
                }

                // When there is no challenge, keep checking every minute
                if (current == null)
                    return TimeSpan.FromMinutes(1);

                // This shouldn't ever happen - the current challenge should always have an end time!
                if (current.EndTime == null)
                    return TimeSpan.FromMinutes(1);

                // Wait until the challenge ends or 5 minutes, whichever is shorest. Never wait less than 30 seconds.
                var duration = current.EndTime.Value - DateTime.UtcNow;
                if (duration > TimeSpan.FromMinutes(5))
                    return TimeSpan.FromMinutes(5);
                if (duration < TimeSpan.FromSeconds(30))
                    return TimeSpan.FromSeconds(30);
                return duration;

            });
        }
    }
}
