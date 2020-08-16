using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Services.Scoring;
using MoreLinq;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Execute;

namespace YololCompetition.Services.Verification
{
    public class YololEmulatorVerification
        : IVerification
    {
        private readonly Configuration _config;
        private readonly IYololExecutor _executor;

        public YololEmulatorVerification(Configuration config, IYololExecutor executor)
        {
            _config = config;
            _executor = executor;
        }

        public async Task<(Success?, Failure?)> Verify(Challenge.Challenge challenge, string yolol)
        {
            await Task.CompletedTask;

            IScore scoreMode = challenge.ScoreMode switch {
                ScoreMode.BasicScoring => new BasicScoring(),
                ScoreMode.Approximate => new ApproximateScoring(),
                ScoreMode.Unknown => throw new InvalidOperationException("Cannot use `Unknown` score mode (challenge is broken - contact Martin#2468)"),
                _ => throw new NotImplementedException($"Score mode `{challenge.ScoreMode}` is not implemented")
            };

            // Retrieve the test cases for the challenge
            var (inputs, outputs) = GetTests(challenge);

            // Check input program fits within 20x70
            var lines = yolol.Split("\n");
            if (lines.Length > 20 || lines.Any(l => l.Length > 70))
                return (null, new Failure(FailureType.ProgramTooLarge, null));

            // parse the program
            var result = Parser.ParseProgram(yolol);
            if (!result.IsOk)
                return (null, new Failure(FailureType.ParseFailed, result.Err.ToString()));

            // Prepare a machine state for execution
            var state = _executor.Prepare(result.Ok, $":{challenge.CheckIndicator}");

            // Begin counting how long it takes to verify (for profiling purposes)
            var timer = new Stopwatch();
            timer.Start();

            // Run through test cases one by one
            var overflowIters = (long)_config.MaxItersOverflow;
            for (var i = 0; i < Math.Max(inputs.Count, outputs.Count); i++)
            {
                // Set inputs (if there are any)
                IReadOnlyDictionary<string, Value> input;
                if (i < inputs.Count)
                {
                    input = inputs[i];
                    foreach (var (key, value) in input)
                        state.TrySet($":{key}", value);
                }
                else
                    input = new Dictionary<string, Value>();

                // Clear completion indicator
                state.Done = false;

                // Run for max allowed number of lines
                var err1 = await state.Run(_config.MaxTestIters, TimeSpan.FromMilliseconds(100));
                if (err1 != null)
                    return (null, new Failure(FailureType.Other, err1));

                // This test case didn't finish yet, run it some more with the overflow pool
                if (!state.Done)
                {
                    var executed = state.TotalLinesExecuted;
                    var err2 = await state.Run((uint)overflowIters, TimeSpan.FromMilliseconds(100));
                    if (err2 != null)
                        return (null, new Failure(FailureType.Other, err2));

                    // Shrink the overflow pool by however many ticks that just used
                    overflowIters -= (uint)(state.TotalLinesExecuted - executed);

                    //Once the overflow pool is empty too, fail
                    if (overflowIters <= 0 || !state.Done)
                        return (null, new Failure(FailureType.RuntimeTooLong, $"Completed {i}/{inputs.Count} tests."));
                }

                // Check this test case with the current scoring mode
                var scoreFailure = scoreMode.CheckCase(input, outputs[i], state);
                if (scoreFailure != null)
                    return (null, scoreFailure);
            }

            Console.WriteLine($"Verified {state.TotalLinesExecuted} ticks, {timer.ElapsedMilliseconds}ms runtime");

            // Calculate score
            var codeLength = yolol.Replace("\n", "").Length;
            var score = scoreMode.FinalizeScore(
                (uint)Math.Max(inputs.Count, outputs.Count),
                (uint)state.TotalLinesExecuted,
                codeLength
            );

            return (new Success(score, (uint)state.TotalLinesExecuted, (uint)codeLength, scoreMode.Hint), null);
        }

        private static (IReadOnlyList<IReadOnlyDictionary<string, Value>>, IReadOnlyList<IReadOnlyDictionary<string, Value>>) GetTests(Challenge.Challenge challenge)
        {
            if (challenge.ShuffleTests)
            {
                var shuffled = challenge.Inputs.Zip(challenge.Outputs).Shuffle().ToArray();
                return (
                    shuffled.Select(a => a.First).ToArray(),
                    shuffled.Select(a => a.Second).ToArray()
                );
            }
            else
            {
                return (challenge.Inputs, challenge.Outputs);
            }
        }
    }
}
