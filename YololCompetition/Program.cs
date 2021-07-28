using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Moserware.Skills;
using YololCompetition.Services.Broadcast;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Cron;
using YololCompetition.Services.Database;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Fleet;
using YololCompetition.Services.Leaderboard;
using YololCompetition.Services.Schedule;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Subscription;
using YololCompetition.Services.Verification;
using YololCompetition.Services.Messages;
using YololCompetition.Services.Parsing;
using YololCompetition.Services.Rates;
using YololCompetition.Services.Status;
using YololCompetition.Services.Trueskill;

namespace YololCompetition
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting...");

            var config = Parser.Default.ParseArguments<Configuration?>(args).MapResult(c => c, ArgsNotParsed);
            if (config == null)
                return;

            var services = BuildServices();
            services.AddSingleton(config);

            var provider = services.BuildServiceProvider();
            services.AddSingleton(provider);

            var commands = provider.GetRequiredService<CommandService>();
            await commands.AddModulesAsync(Assembly.GetExecutingAssembly(), provider);

            var bot = provider.GetRequiredService<DiscordBot>();
            await bot.Start();

            var messages = provider.GetRequiredService<IMessages>();
            messages.StartMessageWatch();

            var battles = provider.GetRequiredService<BattleQueueExecutor>();
            battles.Start();

            var status = provider.GetRequiredService<IStatusUpdater>();
            status.Start();

            Console.WriteLine("Bot Started");
            await provider.GetRequiredService<IScheduler>().Start();
        }

        private static Configuration? ArgsNotParsed(IEnumerable<Error> errs)
        {
            foreach (var err in errs)
                Console.WriteLine(err.ToString());

            return null;
        }

        private static IServiceCollection BuildServices()
        {
            var di = new ServiceCollection();
            di.AddSingleton<IServiceCollection>(di);

            di.AddSingleton(new CommandService(new CommandServiceConfig {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                ThrowOnError = true
            }));
            di.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 0,
            }));
            di.AddSingleton<DiscordBot>();
            di.AddSingleton<IScheduler, InMemoryScheduler>();
            di.AddSingleton<ICron, InMemoryCron>();
            di.AddSingleton<IRateLimit, InMemoryRateLimits>();
            di.AddSingleton<InteractiveService>();

            //di.AddTransient<IYololExecutor, YololInterpretExecutor>();
            di.AddTransient<IYololExecutor, YololCompileExecutor>();

            di.AddTransient<IBroadcast, DiscordBroadcast>();
            di.AddTransient<IDatabase, SqliteDatabase>();
            di.AddTransient<ILeaderboard, DbLeaderboard>();
            di.AddTransient<ISolutions, DbSolutions>();
            di.AddTransient<IChallenges, DbChallenges>();
            di.AddTransient<ISubscription, DbSubscription>();
            di.AddTransient<IMessages, DbMessages>();
            di.AddTransient<ITrueskill, DbTrueskill>();
            di.AddTransient<IYololParser, YololEmulatorParser>();
            di.AddTransient<IStatusUpdater, SchedulerStatus>();

            di.AddTransient<IFleetStorage, DbFleetStorage>();
            di.AddTransient<IFleetRankings, DbFleetRankings>();
            di.AddTransient<IFleetBattleQueue, DbFleetBattleQueue>();
            di.AddSingleton<BattleQueueExecutor>();

            di.AddSingleton(GameInfo.DefaultGameInfo);
            di.AddTransient<ITrueskillUpdater, MoserwareTrueskillUpdater>();

            di.AddTransient<IVerification, BasicVerification>();

            return di;
        }
    }
}
