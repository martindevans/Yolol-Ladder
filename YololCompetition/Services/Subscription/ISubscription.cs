using System.Collections.Generic;
using System.Threading.Tasks;

namespace YololCompetition.Services.Subscription
{
    public interface ISubscription
    {
        public Task Subscribe(ulong channel, ulong guild);

        public Task Unsubscribe(ulong channel, ulong guild);

        public IAsyncEnumerable<Subscription> GetSubscriptions();
    }

    public readonly struct Subscription
    {
        public ulong Channel { get; }
        public ulong Guild { get; }

        public Subscription(ulong channel, ulong guild)
        {
            Channel = channel;
            Guild = guild;
        }
    }
}
