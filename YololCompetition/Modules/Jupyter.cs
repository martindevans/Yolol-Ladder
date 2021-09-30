using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.Commands;
using JetBrains.Annotations;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Jupyter;

namespace YololCompetition.Modules
{
    [Hidden]
    [UsedImplicitly]
    //[RequireContext(ContextType.DM | ContextType.Group)]
    [Group("jupyter")]
    public class Jupyter
        : InteractiveBase
    {
        private readonly IYololExecutor _executor;

        public Jupyter(IYololExecutor executor)
        {
            _executor = executor;
        }

        [Command("start")]
        public async Task NewContext()
        {
            var ctx = new JupyterContext(_executor);
            await ReplyAsync("Starting interactive Yolol session. Type `exit` to terminate session.");
            await ctx.Run(this);
        }
    }
}
