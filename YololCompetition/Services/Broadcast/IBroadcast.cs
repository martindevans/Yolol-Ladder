using System.Threading.Tasks;
using Discord;

namespace YololCompetition.Services.Broadcast
{
    public interface IBroadcast
    {
        public Task Broadcast(Embed embed);

        public Task Broadcast(string message);
    }
}
