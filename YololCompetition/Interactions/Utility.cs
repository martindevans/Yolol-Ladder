using System.Threading.Tasks;
using Discord.Interactions;

namespace YololCompetition.Interactions
{
    public class Utility
        : InteractionModuleBase
    {
        [SlashCommand("ping", "Test if I am alive")]
        public async Task Ping()
        {
            await RespondAsync("Pong");

            
        }
    }
}
