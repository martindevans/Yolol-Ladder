﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Services.Scoring;
using MoreLinq;
using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Verification
{
    public class YololEmulatorVerification
        : IVerification
    {
        private readonly Configuration _config;

        public YololEmulatorVerification(Configuration config)
        {
            _config = config;
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
            var entry = result.Ok;
            
            // Get the variable which the program uses to indicate it is ready to move to the next round
            var state = new MachineState(new DefaultValueDeviceNetwork());
            var done = state.GetVariable($":{challenge.CheckIndicator}");

            // Begin counting how long it takes to verify (for profiling purposes)
            var timer = new Stopwatch();
            timer.Start();

            // Run through test cases one by one
            var overflowIters = _config.MaxItersOverflow;
            var totalRuntime = 0u;
            var pc = 0;
            for (var i = 0; i < Math.Min(inputs.Count, outputs.Count); i++)
            {
                // Set inputs
                var input = inputs[i];
                foreach (var (key, value) in input)
                    state.GetVariable($":{key}").Value = value;

                // Clear completion indicator
                done.Value = 0;

                // Run lines until completion indicator is set or execution time limit is exceeded
                var limit = 0;
                while (!done.Value.ToBool())
                {
                    // Check if this test has exceed it's time limit
                    if (limit++ > _config.MaxTestIters)
                    {
                        //If so use iterations from the overflow pool
                        overflowIters--;

                        //Once the overflow pool is empty too, fail
                        if (overflowIters <= 0)
                            return (null, new Failure(FailureType.RuntimeTooLong, null));
                    }

                    totalRuntime++;
                    try
                    {
                        // If line if blank, just move to the next line
                        if (pc >= entry.Lines.Count)
                            pc++;
                        else
                            pc = entry.Lines[pc].Evaluate(pc, state);
                    }
                    catch (ExecutionException)
                    {
                        pc++;
                    }

                    // loop around if program counter goes over max
                    if (pc >= 20)
                        pc = 0;
                }

                // Check this test case with the current scoring mode
                var scoreFailure = scoreMode.CheckCase(input, outputs[i], state);
                if (scoreFailure != null)
                    return (null, scoreFailure);
            }

            Console.WriteLine($"Verified {totalRuntime} ticks, {timer.ElapsedMilliseconds}ms runtime");

            // Calculate score
            var codeLength = yolol.Replace("\n", "").Length;
            var score = scoreMode.FinalizeScore(
                (uint)inputs.Count,
                totalRuntime,
                codeLength
            );

            return (new Success(score, totalRuntime, (uint)codeLength, scoreMode.Hint), null);
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
