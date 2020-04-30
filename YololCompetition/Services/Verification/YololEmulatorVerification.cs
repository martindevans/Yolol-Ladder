using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Services.Scoring;
using MoreLinq;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Verification
{
    public class YololEmulatorVerification
        : IVerification
    {
        private readonly Configuration _config;
        private readonly IScore _score;

        public YololEmulatorVerification(Configuration config, IScore score)
        {
            _config = config;
            _score = score;
        }

        public async Task<(Success?, Failure?)> Verify(Challenge.Challenge challenge, string yolol)
        {
            await Task.CompletedTask;

            if (challenge.ScoreMode != Challenge.ScoreMode.BasicScoring)
                throw new NotImplementedException($"Score mode `{challenge.ScoreMode}` is not implemented");

            // Retrieve the test cases for the challenge
            var (inputs, outputs) = GetTests(challenge);

            // Check input program is 20x70
            var lines = yolol.Split("\n");
            if (lines.Length > 20 || lines.Any(l => l.Length > 70))
                return (null, new Failure(FailureType.ProgramTooLarge, null));

            // parse the entry program
            var result = Parser.ParseProgram(yolol);
            if (!result.IsOk)
                return (null, new Failure(FailureType.ParseFailed, result.Err.ToString()));

            var entry = result.Ok;
            
            // Get the variable which the program uses to indicate it is ready to move to the next round
            var state = new MachineState(new DefaultValueDeviceNetwork());
            var done = state.GetVariable($":{challenge.CheckIndicator}");

            // Run through test cases one by one
            var totalRuntime = 0;
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
                    if (limit++ > _config.MaxTestIters)
                        return (null, new Failure(FailureType.RuntimeTooLong, null));

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

                // Check outputs
                foreach (var (key, value) in outputs[i])
                {
                    var v = state.GetVariable($":{key}");
                    if ((v.Value != value).ToBool())
                    {
                        var ii = string.Join(",", input.Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));
                        var oo = string.Join(",", outputs[i].Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));

                        return (null, new Failure(FailureType.IncorrectResult, $"For inputs {ii} expected outputs {oo}, got `{v.Value.ToHumanString()}` for `:{key}`"));
                    }
                }
            }

            // Calculate score
            var codeLength = yolol.Replace("\n", "").Length;
            var score = _score.Score(
                challenge.Difficulty,
                _config.MaxTestIters * Math.Min(inputs.Count, outputs.Count),
                totalRuntime,
                codeLength
            );

            return (new Success((uint)score, (uint)totalRuntime, (uint)codeLength), null);
        }

        private (IReadOnlyList<IReadOnlyDictionary<string, Value>>, IReadOnlyList<IReadOnlyDictionary<string, Value>>) GetTests(Challenge.Challenge challenge)
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
