using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;

namespace YololCompetition.Extensions
{
    public static class CommandInfoExtensions
    {
        public static async Task<bool> CheckCommandPreconditions([NotNull] this CommandInfo command, [NotNull] ICommandContext context, [NotNull] IServiceProvider services)
        {
            var conditions = command.Preconditions.Concat(command.Module.Preconditions);

            foreach (var precondition in conditions)
                if (!(await precondition.CheckPermissionsAsync(context, command, services)).IsSuccess)
                    return false;

            return true;
        }
    }
}
