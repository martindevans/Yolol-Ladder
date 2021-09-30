using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace YololCompetition.Services.Broadcast
{
    public interface IBroadcast
    {
        public IAsyncEnumerable<IUserMessage> Broadcast(Embed embed);

        public Task Broadcast(string message);
    }
}
