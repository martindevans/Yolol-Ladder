using System;
using System.Threading.Tasks;
using Discord;

namespace YololCompetition.Services.Interactive
{
    public interface IInteractive
    {
        Task<IMessage?> NextMessageAsync(IUser source, IChannel channel, TimeSpan timeout);

        void Start();
    }
}
