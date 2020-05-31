using System.Collections.Generic;
using System.Threading.Tasks;


namespace YololCompetition.Services.Messages
{
    public interface IMessages
    {
        Task TrackMessage(ulong ChannelID, ulong MessageID, ulong ChallengeID, MessageType MessageType);
        IAsyncEnumerable<Message> GetCurrentMessages(ulong challengeID);
        void StartMessageWatch();
        Task FinalUpdateMessages();
    }

    public readonly struct Message
    {
        public ulong ChannelID { get; }
        public ulong ChallengeID { get; }
        public ulong MessageID { get; }
        public MessageType MessageType { get; }

        public Message(ulong channel,ulong message, ulong challenge, MessageType type)
        {
            ChannelID = channel;
            MessageID = message;
            ChallengeID = challenge;
            MessageType = type;
        }
    }

    public enum MessageType : uint {
        Current, 
        Leaderboard}
}
