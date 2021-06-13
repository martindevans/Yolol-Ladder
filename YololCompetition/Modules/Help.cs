//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Discord;
//using Discord.Commands;
//using JetBrains.Annotations;
//using YololCompetition.Extensions;

//namespace YololCompetition.Modules
//{
//    [UsedImplicitly]
//    public class Help
//        : ModuleBase
//    {
//        private readonly CommandService _commands;
//        private readonly IServiceProvider _services;
//        private readonly Configuration _config;

//        public Help(CommandService commands, IServiceProvider services, Configuration config)
//        {
//            _commands = commands;
//            _services = services;
//            _config = config;
//        }

//        [Command("help"), Summary("Print all commands")]
//        public async Task GetHelp()
//        {
//            var modules = (await FindModulesAndCommands()).ToArray();

//            var embed = new EmbedBuilder();
//            embed.WithTitle("Yolol Ladder Help");
//            foreach (var (module, commands) in modules)
//            {
//                foreach (var command in commands)
//                {
//                    if (module.Group != null)
//                    {
//                        embed.AddField($"{_config.Prefix}{module.Group} {command.Name}", string.IsNullOrWhiteSpace(command.Summary) ? "Unknown? Help summary required" : command.Summary);
//                    }
//                    else
//                    {
//                        embed.AddField($"{_config.Prefix}{command.Name}", string.IsNullOrWhiteSpace(command.Summary) ? "Unknown? Help summary required" : command.Summary);
//                    }
//                }
//            }

//            await ReplyAsync(embed: embed.Build());
//        }

//        /// <summary>
//        /// Find modules which have at least one command the user can execute
//        /// </summary>
//        private async Task<IReadOnlyDictionary<ModuleInfo, IReadOnlyList<CommandInfo>>> FindModulesAndCommands()
//        {
//            // Find modules
//            var modules = _commands.Modules.Distinct();

//            // Filter to modules which have commands we are allowed to execute
//            var output = new Dictionary<ModuleInfo, IReadOnlyList<CommandInfo>>();
//            foreach (var module in modules)
//            {
//                if (module.Attributes.Any(a => a is HiddenAttribute))
//                    continue;

//                var commands = new List<CommandInfo>();
//                foreach (var cmd in module.Commands)
//                {
//                    if (cmd.Attributes.Any(a => a is HiddenAttribute))
//                        continue;
//                    if (await cmd.CheckCommandPreconditions(Context, _services))
//                        commands.Add(cmd);
//                }

//                if (commands.Any())
//                    output.Add(module, commands);
//            }

//            return output;
//        }
//    }
//}
