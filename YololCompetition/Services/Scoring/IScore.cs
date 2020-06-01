using System.Collections.Generic;
using Yolol.Execution;
using YololCompetition.Services.Verification;

namespace YololCompetition.Services.Scoring
{
    public interface IScore
    {
        /// <summary>
        /// Update the score with points for accuracy
        /// </summary>
        /// <param name="inputs"></param>
        /// <param name="expectedOutputs"></param>
        /// <param name="state"></param>
        Failure? CheckCase(IReadOnlyDictionary<string, Value> inputs, IReadOnlyDictionary<string, Value> expectedOutputs, MachineState state);

        /// <summary>
        /// Return the final score for this challenge
        /// </summary>
        /// <param name="totalTests">Total number of test cases</param>
        /// <param name="totalTicks">Total ticks used</param>
        /// <param name="codeChars">Total number of characters used</param>
        /// <returns></returns>
        uint FinalizeScore(uint totalTests, uint totalTicks, int codeChars);
    }
}
