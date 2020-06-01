using System;
using System.Collections.Generic;
using Yolol.Execution;
using YololCompetition.Extensions;
using YololCompetition.Services.Verification;
using Type = Yolol.Execution.Type;

namespace YololCompetition.Services.Scoring
{
    public class ApproximateScoring
        : BasicScoring
    {
        public const double AccuracyPoints = PointsPerTick * 10;

        // Max multiplier for an almost accurate answer (0.001) is 6
        // Getting a better answer than that is worth this much:
        public const double ExactMultiplier = 8;

        public override Failure? CheckCase(IReadOnlyDictionary<string, Value> inputs, IReadOnlyDictionary<string, Value> expectedOutputs, MachineState state)
        {
            // Check that the machine state is exactly correct for every expected output
            foreach (var (key, expected) in expectedOutputs)
            {
                var actual = state.GetVariable($":{key}").Value;

                // Add bonus points averaged across all cases
                AddBonusAveragedPoints((expected.Type, actual.Type) switch {
                    (Type.Number, Type.Number) => AccuracyScore(expected.Number, actual.Number),
                    (Type.String, Type.String) => AccuracyScore(expected.String, actual.String),
                    _ => 0,
                });
            }

            // Approx challenge never fails
            return null;
        }

        private static double AccuracyScore(Number a, Number b)
        {
            var error = Math.Abs((double)(a.Value - b.Value));
            if (Math.Abs(error) < 0.001)
                return AccuracyPoints * ExactMultiplier;

            return Math.Max(0, 3 - Math.Log10(error)) * AccuracyPoints;
        }

        private static double AccuracyScore(string a, string b)
        {
            if (a.Equals(b))
                return AccuracyPoints * 20;

            return a.Levenshtein(b) / (double)Math.Max(a.Length, b.Length) * 10;
        }
    }
}
