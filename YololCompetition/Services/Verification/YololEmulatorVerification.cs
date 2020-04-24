using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Services.Scoring;

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

            // Check input program is 20x70
            var lines = yolol.Split("\n");
            if (lines.Length > 20 || lines.Any(l => l.Length > 70))
                return (null, new Failure(FailureType.ProgramTooLarge));

            // parse the entry program
            var entry = Parse(yolol);
            if (entry == null)
                return (null, new Failure(FailureType.ParseFailed));

            // Get the variable which the program uses to indicate it is ready to move to the next round
            var state = new MachineState(new DefaultValueDeviceNetwork());
            var done = state.GetVariable($":{challenge.CheckIndicator}");

            // Run through test cases one by one
            var totalRuntime = 0;
            var pc = 0;
            for (var i = 0; i < Math.Min(challenge.Inputs.Count, challenge.Outputs.Count); i++)
            {
                // Set inputs
                var inputs = challenge.Inputs[i];
                foreach (var (key, value) in inputs)
                    state.GetVariable($":{key}").Value = value;

                // Clear completion indicator
                done.Value = 0;

                // Run lines until completion indicator is set or execution time limit is exceeded
                var limit = 0;
                while (!done.Value.ToBool())
                {
                    if (limit++ > _config.MaxTestIters)
                        return (null, new Failure(FailureType.RuntimeTooLong));

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
                foreach (var (key, value) in challenge.Outputs[i])
                {
                    var v = state.GetVariable($":{key}");
                    if ((v.Value != value).ToBool())
                        return (null, new Failure(FailureType.IncorrectResult));
                }
            }

            // Calculate score
            var codeLength = yolol.Replace("\n", "").Length;
            var score = _score.Score(
                challenge.Difficulty,
                _config.MaxTestIters * Math.Min(challenge.Inputs.Count, challenge.Outputs.Count),
                totalRuntime,
                codeLength
            );

            return (new Success((uint)score, (uint)totalRuntime, (uint)codeLength), null);
        }

        private static Yolol.Grammar.AST.Program? Parse(string code)
        {
            var result = Parser.ParseProgram(code);
            if (!result.IsOk)
                return null;

            return result.Ok;
        }
    }
}
