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
    public class BasicVerification
        : IVerification
    {
        // How many extra iters (across all tests) may be used
        public const int MaxItersOverflow = 10000;

        // Max lines executed per test case
        public const uint MaxTestIters = 1000;

        private readonly IYololExecutor _executor;

        public BasicVerification(IYololExecutor executor)
        {
            _executor = executor;
        }

        public async Task<(Success?, Failure?)> Verify(Challenge.Challenge challenge, string yolol)
        {
            await Task.CompletedTask;

            IScore scoreMode = challenge.ScoreMode switch {
                ScoreMode.BasicScoring => new BasicScoring(),
                ScoreMode.Approximate => new ApproximateScoring(),
                ScoreMode.Unknown => throw new InvalidOperationException("Cannot use `Unknown` score mode (challenge is broken - contact Martin#2468)"),
                _ => throw new NotImplementedException($"Score mode `{challenge.ScoreMode}` is not implemented (contact Martin#2468)")
            };

            if (challenge.Chip == YololChip.Unknown)
                throw new InvalidOperationException("Cannot submit to a challenge with `YololChip.Unknown` (challenge is broken - contact Martin#2468)");

            // Check input program fits within 20x70
            var lines = yolol.Split("\n");
            if (lines.Length > 20 || lines.Any(l => l.Length > 70))
                return (null, new Failure(FailureType.ProgramTooLarge, null));

            // parse the program
            var parsed = Parser.ParseProgram(yolol);
            if (!parsed.IsOk)
                return (null, new Failure(FailureType.ParseFailed, parsed.Err.ToString()));

            // Verify that code is allowed on the given chip level
            if (challenge.Chip != YololChip.Professional)
            {
                var fail = CheckChipLevel(challenge.Chip, parsed.Ok);
                if (fail != null)
                    return (null, fail);
            }

            // Prepare a machine state for execution.
            // Two states are needed - one for user code and one for code supplied by the challenge
            var stateUser = _executor.Prepare(parsed.Ok, $":{challenge.CheckIndicator}");
            var stateChallenge = _executor.Prepare(challenge.Intermediate);

            // Retrieve the test cases for the challenge
            var (inputs, outputs) = GetTests(challenge);

            // Begin counting how long it takes to verify (for profiling purposes)
            var timer = new Stopwatch();
            timer.Start();

            // Run through test cases one by one
            var overflowIters = (long)MaxItersOverflow;
            for (var i = 0; i < Math.Max(inputs.Count, outputs.Count); i++)
            {
                // Set inputs in user execution state
                var input = SetInputs(i < inputs.Count ? inputs[i] : new Dictionary<string, Value>(), stateUser);

                // Run the user code until completion
                var failure = RunToDone(stateUser, MaxTestIters, i, inputs.Count, ref overflowIters);
                if (failure != null)
                    return (null, failure);

                // Copy all externals user->challenge
                stateUser.CopyTo(stateChallenge, true);

                // Set the challenge inputs _and_ outputs into the challenge state
                SetInputs(input, stateChallenge, "input_");
                SetInputs(outputs[i], stateChallenge, "output_");

                // Run the challenge code
                var overflow = (long)MaxItersOverflow;
                failure = RunToDone(stateChallenge, MaxTestIters, 0, 0, ref overflow);
                if (failure != null)
                    return (null, new Failure(FailureType.ChallengeCodeFailed, failure.Hint));

                // Copy all externals challenge->user
                stateChallenge.CopyTo(stateUser, true);

                // Check if the challenge code has forced a failure
                var forceFail = stateChallenge.TryGet(new VariableName(":fail"));
                if (forceFail?.Type == Yolol.Execution.Type.String && forceFail.Value.String.Length != 0)
                    return (null, new Failure(FailureType.ChallengeForceFail, forceFail.Value.String.ToString()));

                // Check this test case with the current scoring mode
                var scoreFailure = scoreMode.CheckCase(input, outputs[i], stateUser);
                if (scoreFailure != null)
                    return (null, scoreFailure);
            }

            Console.WriteLine($"Verified {stateUser.TotalLinesExecuted} ticks, {timer.ElapsedMilliseconds}ms runtime");

            // Calculate score
            var codeLength = yolol.Replace("\n", "").Length;
            var score = scoreMode.FinalizeScore(
                (uint)Math.Max(inputs.Count, outputs.Count),
                (uint)stateUser.TotalLinesExecuted,
                codeLength
            );

            return (new Success(score, (uint)stateUser.TotalLinesExecuted, (uint)codeLength, scoreMode.Hint, (uint)challenge.Inputs.Count), null);
        }

        private static IReadOnlyDictionary<string, Value> SetInputs(IReadOnlyDictionary<string, Value> values, IExecutionState state, string prefix = ":")
        {
            foreach (var (k, v) in values)
                state.Set(new VariableName(prefix + k), v);

            return values;
        }

        private static Failure? RunToDone(IExecutionState state, uint maxTestIters, int testIndex, int testCount, ref long overflowIters)
        {
            // Clear completion indicator
            state.Done = false;

            // Run for max allowed number of lines
            var err1 = state.Run(maxTestIters, TimeSpan.FromMilliseconds(600));
            if (err1 != null)
                return new Failure(FailureType.Other, err1);

            // This test case didn't finish yet, run it some more with the overflow pool
            if (!state.Done)
            {
                var executed = state.TotalLinesExecuted;
                var err2 = state.Run((uint)overflowIters, TimeSpan.FromMilliseconds(600));
                if (err2 != null)
                    return new Failure(FailureType.Other, err2);

                // Shrink the overflow pool by however many ticks that just used
                overflowIters -= (uint)(state.TotalLinesExecuted - executed);

                //Once the overflow pool is empty too, fail
                if (overflowIters <= 0 || !state.Done)
                    return new Failure(FailureType.RuntimeTooLong, $"Completed {testIndex}/{testCount} tests.");
            }

            return null;
        }

        private static Failure? CheckChipLevel(YololChip level, Yolol.Grammar.AST.Program program)
        {
            if (level == YololChip.Unknown || level == YololChip.Professional)
                return null;

            var statements = from line in program.Lines
                             from stmt in line.Statements.Statements
                             select stmt;

            var check = new ChipLevelChecker(level);
            foreach (var statement in statements)
                if (!check.Visit(statement))
                    return new Failure(FailureType.InvalidProgramForChipType, null);

            return null;
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
