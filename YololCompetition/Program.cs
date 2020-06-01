using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using YololCompetition.Services.Broadcast;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Cron;
using YololCompetition.Services.Database;
using YololCompetition.Services.Leaderboard;
using YololCompetition.Services.Schedule;
using YololCompetition.Services.Scoring;
using YololCompetition.Services.Solutions;
using YololCompetition.Services.Subscription;
using YololCompetition.Services.Verification;
using YololCompetition.Services.Messages;

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

            var commands = provider.GetService<CommandService>();
            await commands.AddModulesAsync(Assembly.GetExecutingAssembly(), provider);

            var messages = provider.GetService<IMessages>();

            var bot = provider.GetService<DiscordBot>();
            await bot.Start();

            messages.StartMessageWatch();

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

            di.AddTransient<IBroadcast, DiscordBroadcast>();
            di.AddTransient<IDatabase, SqliteDatabase>();
            di.AddTransient<ILeaderboard, DbLeaderboard>();
            di.AddTransient<ISolutions, DbSolutions>();
            di.AddTransient<IChallenges, DbChallenges>();
            di.AddTransient<ISubscription, DbSubscription>();
            di.AddTransient<IMessages, DbMessages>();

            di.AddTransient<IVerification, YololEmulatorVerification>();

            return di;
        }
    }
}
