using System;

namespace YololCompetition.Services.Scoring
{
    public class BasicScoring
        : IScore
    {
        private const int PointsPerChar = 1;
        private const int PointsPerTick = 50;

        private const int MaxChars = 20 * 70;
        private const int MaxTicksScore = 1000 * PointsPerTick;
        private const int MaxScore = MaxChars * PointsPerChar + MaxTicksScore;
        

        public uint Score(uint totalTests, uint totalTicks, int codeChars)
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
                score -= Math.Clamp(avgTicks * PointsPerTick, 0, MaxTicksScore);

                //Truncate to an integer score
                return (uint)score;
            }
        }
    }
}
