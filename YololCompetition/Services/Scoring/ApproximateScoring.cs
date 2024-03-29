﻿using System;
using System.Collections.Generic;
using System.Linq;
using BlazorYololEmulator.Shared;
using F23.StringSimilarity;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Extensions;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Verification;
using Type = Yolol.Execution.Type;

namespace YololCompetition.Services.Scoring
{
    public class ApproximateScoring
        : BasicScoring
    {
        public const double AccuracyPoints = PointsPerTick * 9;

        // Max multiplier for an almost accurate answer (0.001) is 6,
        // Getting a better answer than that is worth this much:
        public const double ExactMultiplier = 7.5f;

        public override Failure? CheckCase(IReadOnlyDictionary<string, Value> inputs, IReadOnlyDictionary<string, Value> expectedOutputs, IExecutionState state, SerializedState? debugState)
        {
            // Check that the machine state is exactly correct for every expected output
            foreach (var (key, expected) in expectedOutputs)
            {
                var actual = state.TryGet(new VariableName($":{key}")) ?? (Value)0;

                // Add bonus points averaged across all cases
                switch (expected.Type, actual.Type)
                {
                    case (Type.Number, Type.Number):
                        AddBonusAveragedPoints(AccuracyScoreNumbers(key, expected.Number, actual.Number));
                        break;

                    case (Type.String, Type.String):
                        AddBonusAveragedPoints(AccuracyScoreStrings(key, expected.String.ToString(), actual.String.ToString()));
                        break;

                    case (_, _):
                        var ii = InputString();
                        var oo = OutputString();
                        return new Failure(FailureType.IncorrectResult, $"For inputs {ii} expected outputs {oo}, got `:{key}={actual.ToHumanString()}`", debugState);
                }
            }

            return null;

            string InputString() => string.Join(",", inputs.Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));

            string OutputString() => string.Join(",", expectedOutputs.Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));

            double AccuracyScoreNumbers(string key, Number expected, Number actual)
            {
                var error = Math.Abs((double)((decimal)expected - (decimal)actual));
                if (Math.Abs(error) < 0.001)
                    return AccuracyPoints * ExactMultiplier;

                _totalError += error;
                if (error > _hintError && error > 0)
                {
                    var ii = InputString();
                    var oo = OutputString();
                    _hint = $"For inputs {ii} expected outputs {oo}, produced `:{key}={new Value(actual).ToHumanString()}` (error of `{error}`).";
                    _hintError = error;
                }

                return Math.Max(0, 3 - Math.Log10(error)) * AccuracyPoints;
            }

            double AccuracyScoreStrings(string key, string expected, string actual)
            {
                var d = new Damerau();
                var error = d.Distance(expected, actual);
                if (error < 1)
                    return AccuracyPoints * ExactMultiplier;

                _totalError += error;
                if (error > _hintError && error > 0)
                {
                    var ii = InputString();
                    var oo = OutputString();
                    _hint = $"For inputs {ii} expected outputs {oo}, produced `:{key}={new Value(actual).ToHumanString()}` (error of `{error}`).";
                    _hintError = error;
                }

                return AccuracyPoints / Math.Pow(error + 1, 1.25);
            }
        }

        public override int FinalizeScore(uint totalTests, uint totalTicks, int codeChars)
        {
            if (_hint != null)
                _hint += $" Total error: {_totalError:#.000}.";

            return base.FinalizeScore(totalTests, totalTicks, codeChars);
        }

        private double _totalError;
        private double _hintError;
        private string? _hint;
        public override string? Hint => _hint;
    }
}
