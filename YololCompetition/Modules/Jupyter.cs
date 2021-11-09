using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Jupyter;

namespace YololCompetition.Modules
{
    [UsedImplicitly]
    [Group("yolol")]
    public class Jupyter
        : InteractiveBase
    {
        private readonly IYololExecutor _executor;

        public Jupyter(IYololExecutor executor)
        {
            _executor = executor;
        }

        [Command("interactive")]
        [Priority(100)]
        public async Task NewContext()
        {
            var ctx = new JupyterContext(_executor);
            await ReplyAsync("Starting interactive Yolol session. Type `exit` to terminate session.");
            await ReplyAsync("Type code in triple backticks. Every new message will add to the program. Edit old messages to update the program.");
            await ctx.Run(this);
        }
    }
}
