using CommandLine;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

namespace YololCompetition
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Configuration
    {
        [Option('t', "token-var", Required = true, HelpText = "Environment var the token is stored in")]
        public string TokenEnvVar { get; set; }

        [Option('p', "prefix", Required = true, HelpText = "Prefix Character")]
        public char Prefix { get; set; }

        [Option('d', "db", Required = true, HelpText = "Database Connection String")]
        public string DatabaseConnectionString { get; set; }

        [Option("duration", Required = false, HelpText = "How long each challenge should last in hours", Default = (uint)72)]
        public uint ChallengeDurationHours { get; set; }

        [Option("replays", Required = true, HelpText = "Location to save fleet replays to")]
        public string ReplayOutputDirectory { get; set; }

        [Option("start-time", Required = false, HelpText = "What time of the day to start a challenge in UTC (in minutes)", Default = (uint)1140)]
        public uint ChallengeStartTime { get; set; }

        [Option("yogi-path", Required = false, HelpText = "File path to the binary for the Yogi execution engine")]
        public string? YogiPath { get; set; }

        [Option("online-debugger-url", Required = false, HelpText = "Base URL to an online debugger")]
        public string? OnlineDebuggerUrl { get; set; }
    }
}
