using System.Threading.Tasks;
using Discord.Interactions;
using JetBrains.Annotations;

namespace YololCompetition.Interactions
{
    [UsedImplicitly]
    public class Utility
        : InteractionModuleBase
    {
        [SlashCommand("ping", "Test if I am alive")]
        [UsedImplicitly]
        public async Task Ping()
        {
            await RespondAsync("Pong");

            
        }
    }
}
