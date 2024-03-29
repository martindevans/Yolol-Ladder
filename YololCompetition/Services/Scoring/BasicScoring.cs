﻿using System;
using System.Collections.Generic;
using System.Linq;
using BlazorYololEmulator.Shared;
using Yolol.Execution;
using Yolol.Grammar;
using YololCompetition.Extensions;
using YololCompetition.Services.Execute;
using YololCompetition.Services.Verification;

namespace YololCompetition.Services.Scoring
{
    public class BasicScoring
        : IScore
    {
        public const int PointsPerChar = 1;
        public const int PointsPerTick = 100;

        private const int MaxChars = 20 * 70;
        private const int MaxTicksScore = 1000 * PointsPerTick;
        private const int MaxScore = MaxChars * PointsPerChar + MaxTicksScore;

        private double _bonusPoints;
        private double _bonusCasePoints;

        public virtual string? Hint => null;

        public virtual int FinalizeScore(uint totalTests, uint totalTicks, int codeChars)
        {
            // Throw an exception if any arithmetic inside this block over/underflows
            checked
            {
                // Start with the maximum score
                var score = (double)MaxScore;

                // Lose 1 point per character used
                score -= Math.Clamp(codeChars, 0, MaxChars) * PointsPerChar;

                // Calculate average number of ticks used per test case
                var avgTicks = (double)totalTicks / totalTests;

                // Subtract off points per tick used
                score -= avgTicks * PointsPerTick;

                // Add bonus points from other sources
                score += _bonusPoints;
                score += _bonusCasePoints / totalTests;

                //Truncate to an integer score
                return (int)Math.Round(score);
            }
        }

        public virtual Failure? CheckCase(IReadOnlyDictionary<string, Value> inputs, IReadOnlyDictionary<string, Value> expectedOutputs, IExecutionState state, SerializedState? debugState)
        {
            // Check that the machine state is exactly correct for every expected output
            foreach (var (key, value) in expectedOutputs)
            {
                var v = state.TryGet(new VariableName($":{key}")) ?? (Value)0;
                if (v != value)
                {
                    var ii = string.Join(",", inputs.Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));
                    var oo = string.Join(",", expectedOutputs.Select(b => $"`:{b.Key}={b.Value.ToHumanString()}`"));
                    return new Failure(FailureType.IncorrectResult, $"For inputs {ii} expected outputs {oo}, got `:{key}={v.ToHumanString()}`", debugState);
                }
            }

            // All variables ok, return null to indicate no failure
            return null;
        }

        protected void AddBonusPoints(double points)
        {
            _bonusPoints += points;
        }

        protected void AddBonusAveragedPoints(double points)
        {
            _bonusCasePoints += points;
        }
    }
}
