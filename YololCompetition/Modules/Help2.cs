using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using YololCompetition.Extensions;

namespace YololCompetition.Modules
{
    public class Help
        : BaseModule
    {
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        private readonly char _prefixCharacter;

        public Help(CommandService commands, IServiceProvider services,  Configuration config)
        {
            _commands = commands;
            _services = services;

            _prefixCharacter = config.Prefix;
        }

        [Command("help")]
        [Summary("List all command modules (groups of commands)")]
        public async Task ListModules()
        {
            string CommandsStr(ModuleInfo module, IEnumerable<CommandInfo> cmds)
            {
                return string.Join(", ", cmds.Select(a => FormatCommandName(a, _prefixCharacter)).Distinct());
            }

            var embed = CreateEmbed(Context, _prefixCharacter, "Command Modules", $"Use `{_prefixCharacter}help name` to find out about a specific command or module");

            var modules = await FindModules();
            foreach (var (module, commands) in modules)
                embed.AddField(module.Name, CommandsStr(module, commands), false);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("help"), Summary("List all commands and modules, filtered by the search string")]
        public async Task ListDetails([Remainder] string search)
        {
            var commands = (await FindCommands(search)).ToArray();
            var modules = (await FindModules(search)).ToArray();

            if (commands[0].Key == 0)
                await ReplyAsync(embed: FormatCommandDetails(Context, _prefixCharacter, commands[0]).Build());
            else if (modules[0].Item1 == 0)
                await ReplyAsync(embed: FormatModuleDetails(Context, _prefixCharacter, modules[0].Item2).Build());
            else
            {
                var items = commands.SelectMany(g => g.Select(c => (g.Key, c.Name))).Concat(modules.Select(m => (m.Item1, m.Item2.Name)))
                                    .DistinctBy(a => a.Name)
                                    .OrderBy(a => a.Item1);
                await ReplyAsync("I can't find a module or command with that name, did you mean one of these: " + string.Join(", ", items.Take(10).Select(m => $"`{m.Name.ToLower()}`")));
            }
        }

        [Command("module")]
        [Summary("I will tell you about the commands in a specific module")]
        public async Task ListModuleDetails([Remainder] string search)
        {
            var modules = (await FindModules(search)).ToArray();
            if (modules[0].Item1 == 0)
                await ReplyAsync(embed: FormatModuleDetails(Context, _prefixCharacter, modules[0].Item2).Build());
            else
                await ReplyAsync("I can't find a module with that name, did you mean one of these: " + string.Join(", ", modules.Take(10).Select(m => $"`{m.Item2.Name.ToLower()}`")));
        }

        [Command("command")]
        [Summary("I will tell you about a specific command")]
        public async Task ListCommandDetails([Remainder] string name)
        {
            var commands = (await FindCommands(name)).ToArray();
            if (commands[0].Key == 0)
            {
                await ReplyAsync(embed: FormatCommandDetails(Context, _prefixCharacter, commands[0]).Build());
            }
            else
            {
                var cs = commands.SelectMany(g => g.Select(c => (g.Key, c.Name))).DistinctBy(c => c.Name);
                await ReplyAsync("I can't find a command with that name, did you mean one of these: " + string.Join(", ", cs.Take(10).Select(c => $"`{c.Name.ToLower()}`")));
            }
        }

        private async Task<IEnumerable<IGrouping<uint, CommandInfo>>> FindCommands(string search)
        {
            return from kvp in await FindModules()
                   let module = kvp.Key
                   from command in kvp.Value
                   let distance = command.Aliases.Append(command.Name).Distinct().Select(a => a.Levenshtein(search)).Min()
                   group command by distance
                   into grp
                   orderby grp.Key
                   select grp;
        }

        private async Task<IEnumerable<(uint, ModuleInfo, IReadOnlyList<CommandInfo>)>> FindModules(string search)
        {
            //Find modules with at least one command we can execute, ordered by levenshtein distance to name or alias
            return from moduleKvp in await FindModules()
                   let module = moduleKvp.Key
                   from moduleName in module.Aliases.Append(module.Name)
                   where !string.IsNullOrEmpty(moduleName)
                   let nameLev = moduleName.ToLower().Levenshtein(search.ToLower())
                   orderby nameLev
                   select (nameLev, module, moduleKvp.Value);
        }

        /// <summary>
        /// Find modules which have at least one command the user can execute
        /// </summary>
        private async Task<IReadOnlyDictionary<ModuleInfo, IReadOnlyList<CommandInfo>>> FindModules()
        {
            //Find non hidden modules
            var modules = _commands
                .Modules
                .Distinct()
                .Where(m => !m.Attributes.OfType<HiddenAttribute>().Any());

            //Filter to modules which have commands we are allowed to execute
            var output = new Dictionary<ModuleInfo, IReadOnlyList<CommandInfo>>();
            foreach (var module in modules)
            {
                var commands = new List<CommandInfo>();
                foreach (var cmd in module.Commands)
                    if (await cmd.CheckCommandPreconditions(Context, _services))
                        commands.Add(cmd);

                if (commands.Any())
                    output.Add(module, commands);
            }

            return output;
        }

        private static EmbedBuilder CreateEmbed(ICommandContext context, char prefix, string title, string description)
        {
            return new EmbedBuilder()
                .WithAuthor(context.Client.CurrentUser)
                .WithFooter($"Use `{prefix}help name` to find out about a specific command or module")
                .WithTitle(title)
                .WithDescription(description);
        }

        private static string FormatCommandName(CommandInfo cmd, char prefix)
        {
            return cmd.Aliases.Count == 1
                ? $"`{prefix}{cmd.Aliases[0].ToLowerInvariant()}`"
                : $"`{prefix}({string.Join('/', cmd.Aliases.Select(a => a.ToLowerInvariant()))})`";
        }

        private static EmbedBuilder FormatCommandDetails(ICommandContext context, char prefix, IEnumerable<CommandInfo> cmds)
        {
            EmbedBuilder SingleCommandDetails(CommandInfo cmd)
            {
                var embed = CreateEmbed(context, prefix, FormatCommandName(cmd, prefix), cmd.Summary ?? cmd.Remarks);

                var example = $"{prefix}{cmd.Aliases[0]} ";
                foreach (var parameterInfo in cmd.Parameters)
                {
                    if (typeof(string).IsAssignableFrom(parameterInfo.Type))
                        example += "\"some text\"";
                    else if (typeof(int).IsAssignableFrom(parameterInfo.Type) || typeof(uint).IsAssignableFrom(parameterInfo.Type) || typeof(long).IsAssignableFrom(parameterInfo.Type))
                        example += 42;
                    else if (typeof(ulong).IsAssignableFrom(parameterInfo.Type))
                        example += 34;
                    else if (typeof(byte).IsAssignableFrom(parameterInfo.Type))
                        example += 17;
                    else if (typeof(IUser).IsAssignableFrom(parameterInfo.Type))
                        example += context.Client.CurrentUser.Mention;
                    else if (typeof(IChannel).IsAssignableFrom(parameterInfo.Type))
                        example += context.Channel.Name;
                    else if (typeof(IRole).IsAssignableFrom(parameterInfo.Type))
                        example += "@rolename";
                    else if (parameterInfo.Type.IsEnum)
                        example += Enum.GetNames(parameterInfo.Type).First();
                    else
                        example += parameterInfo.Type.Name;
                }
                embed.AddField("Example", example);

                foreach (var parameter in cmd.Parameters)
                {
                    var name = parameter.Type.Name;
                    if (parameter.IsOptional)
                        name += " (optional)";

                    var description = $"`{parameter.Name}` {parameter.Summary}";
                    if (parameter.IsOptional)
                        description += $" (default=`{parameter.DefaultValue}`)";

                    embed.AddField($"{name}", $"{description}");
                }

                return embed;
            }

            EmbedBuilder MultiCommandDetails(IReadOnlyCollection<CommandInfo> multi)
            {
                var embed = CreateEmbed(context, prefix, $"{multi.Count} commands", "");

                foreach (var item in multi)
                    embed.AddField(FormatCommandName(item, prefix), item.Summary ?? item.Remarks ?? $"No description :confused:");

                return embed;
            }

            var cmdArr = cmds.ToArray();
            return cmdArr.Length == 1
                 ? SingleCommandDetails(cmdArr[0])
                 : MultiCommandDetails(cmdArr);
        }

        private static EmbedBuilder FormatModuleDetails(ICommandContext context, char prefix, ModuleInfo module)
        {
            return CreateEmbed(context, prefix, module.Name, module.Summary ?? module.Remarks)
                .AddField("Commands:", string.Join(", ", module.Commands.Select(a => FormatCommandName(a, prefix))));
        }
    }
}
