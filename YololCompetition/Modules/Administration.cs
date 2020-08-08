using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class Administration
        : ModuleBase
    {
        [Command("kill"), RequireOwner, Summary("Immediately kill the bot")]
        public async Task Kill(int exitCode = 1)
        {
            await Task.CompletedTask;
            Environment.Exit(exitCode);
        }
    }
}
