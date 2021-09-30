using System.Data.Common;
using System.Threading.Tasks;
using Discord;

namespace YololCompetition.Services.Messages
{
    public interface IMessages
    {
        Task TrackMessage(IUserMessage message, ulong challengeID, MessageType messageType);

        void StartMessageWatch();
    }

    public readonly struct Message
    {
        public ulong ChannelID { get; }
        public ulong ChallengeID { get; }
        public ulong MessageID { get; }
        public MessageType MessageType { get; }

        public Message(ulong channel, ulong message, ulong challenge, MessageType type)
        {
            ChannelID = channel;
            MessageID = message;
            ChallengeID = challenge;
            MessageType = type;
        }

        public static Message Parse(DbDataReader reader)
        {
            return new Message(
                ulong.Parse(reader["ChannelID"].ToString()!),
                ulong.Parse(reader["MessageID"].ToString()!),
                ulong.Parse(reader["ChallengeID"].ToString()!),
                (MessageType)uint.Parse(reader["MessageType"].ToString()!)
            );
        }
    }

    public enum MessageType : uint {
        Current, 
        Leaderboard
    }
}
