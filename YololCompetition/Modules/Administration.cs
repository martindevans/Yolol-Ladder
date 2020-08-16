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
            await ReplyAsync("x_x");
            Environment.Exit(exitCode);
        }
    }
}
