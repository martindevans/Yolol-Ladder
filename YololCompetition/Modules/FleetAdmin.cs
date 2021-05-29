using System;
using System.IO;
using System.Linq;
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
        private readonly Configuration _config;
        private readonly IFleetBattleQueue _queue;

        public FleetAdmin(Configuration config, IFleetBattleQueue queue)
        {
            _config = config;
            _queue = queue;
        }

        [Command("sim_all"), Summary("Fight every fleet against every other fleet")]
        public async Task SimAll()
        {
            throw new NotImplementedException();
        }

        [Command("trim"), Summary("Delete replays older than N days.")]
        public async Task Trim(int days)
        {
            var found = 0;
            var failed = 0;

            var now = DateTime.UtcNow;
            foreach (var replay in Directory.EnumerateFiles(_config.ReplayOutputDirectory, "*.json.deflate").Select(a => new FileInfo(a)).ToList())
            {
                try
                {
                    if (now - replay.CreationTimeUtc >= TimeSpan.FromDays(days))
                    {
                        found++;
                        replay.Delete();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    failed++;
                }
            }

            await ReplyAsync($"Found {found} replays, failed to delete {failed}");
        }
    }
}
