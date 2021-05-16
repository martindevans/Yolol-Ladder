using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json;
using ShipCombatCore.Simulation;

namespace YololCompetition.Services.Fleet
{
    public class BattleQueueExecutor
    {
        private readonly Configuration _config;
        private readonly DiscordSocketClient _client;
        private readonly IFleetBattleQueue _queue;
        private readonly IFleetStorage _fleets;
        private readonly IFleetRankings _ranks;

        public BattleQueueExecutor(Configuration config, DiscordSocketClient client,  IFleetBattleQueue queue, IFleetStorage fleets, IFleetRankings ranks)
        {
            _config = config;
            _client = client;
            _queue = queue;
            _fleets = fleets;
            _ranks = ranks;
        }

        public void Start()
        {
            Task.Factory.StartNew(Run);
        }

        private async Task Run()
        {
            while (true)
            {
                try
                {
                    var battle = await _queue.GetNextBattle();
                    if (battle.HasValue)
                    {
                        await _queue.Remove(battle.Value);
                        await RunSingle(battle.Value);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
            // ReSharper disable once FunctionNeverReturns
        }

        private async Task RunSingle(Battle battle)
        {
            var fa = await _fleets.Load(battle.A);
            var fb = await _fleets.Load(battle.B);
            if (!fa.HasValue || !fb.HasValue)
                return;

            var a = await LoadFleet(battle.A);
            if (a == null)
                return;

            var b = await LoadFleet(battle.B);
            if (b == null)
                return;

            var sim = new Simulation(a, b);
            var report = sim.Run();

            var filename = $"{await fa.Value.FormattedName(_client)} vs {await fb.Value.FormattedName(_client)} ({DateTime.UtcNow:u})";
            await using (var file = File.Create(Path.Combine(_config.ReplayOutputDirectory, filename)))
            await using (var zip = new DeflateStream(file, CompressionLevel.Optimal))
            await using (var stream = new StreamWriter(zip))
            using (var writer = new JsonTextWriter(stream) { Formatting = Formatting.Indented })
            {
                report.Serialize(writer);
                await writer.FlushAsync();
            }

            // Update ranking
            switch (report.Winner)
            {
                case 0:
                    await _ranks.Update(battle.A, battle.B, false);
                    break;
                case 1:
                    await _ranks.Update(battle.B, battle.A, false);
                    break;
                default:
                    await _ranks.Update(battle.A, battle.B, true);
                    break;
            }
        }

        private async Task<ShipCombatCore.Model.Fleet?> LoadFleet(ulong id)
        {
            var bytes = await _fleets.LoadBlob(id);
            if (bytes == null)
                return null;

            await using var stream = new MemoryStream(bytes);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

            return ShipCombatCore.Model.Fleet.TryLoadZip(zip);
        }
    }
}
