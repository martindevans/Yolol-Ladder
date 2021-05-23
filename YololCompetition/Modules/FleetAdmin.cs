using System;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Services.Fleet;

namespace YololCompetition.Modules
{
    [Hidden, UsedImplicitly, RequireOwner]
    [Group("fleet")]
    public class FleetAdmin
        : BaseModule
    {
        private readonly IFleetBattleQueue _queue;

        public FleetAdmin(IFleetBattleQueue queue)
        {
            _queue = queue;
        }

        [Command("trim"), Summary("Trim replays older than N days.")]
        public async Task Trim(int days)
        {
            throw new NotImplementedException();
        }
    }
}
