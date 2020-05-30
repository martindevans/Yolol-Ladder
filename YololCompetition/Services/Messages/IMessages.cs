using System.Collections.Generic;
using System.Threading.Tasks;


namespace YololCompetition.Services.Messages
{
    public interface IMessages
    {
        Task TrackMessage(ulong ChannelID, ulong MessageID, ulong ChallengeID, uint MessageType);
        IAsyncEnumerable<Message> GetCurrentMessages(ulong challengeID);
        void StartMessageWatch();
    }

    public readonly struct Message
    {
        public ulong ChannelID { get; }
        public ulong ChallengeID { get; }
        public ulong MessageID { get; }
        public uint MessageType { get; }

        public Message(ulong channel,ulong message, ulong challenge, uint type)
        {
            ChannelID = channel;
            MessageID = message;
            ChallengeID = challenge;
            MessageType = type;

        }
    }
}