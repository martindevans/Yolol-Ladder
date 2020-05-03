using System;
using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Scoring
{
    public class BasicScoring
        : IScore
    {
        private const int MaxCharsScore = 20 * 70;
        private const int MaxRuntimeScore = MaxCharsScore * 10;

        public uint Score(ChallengeDifficulty difficulty, long maxIters, int runtime, int codeChars)
        {
            checked
            {
                var itersSpare = (double)Math.Max(0, maxIters - runtime);
                var charsSpare = (double)Math.Max(0, MaxCharsScore - codeChars);

                var score = (itersSpare / maxIters) * MaxRuntimeScore 
                          + charsSpare;

                return (uint)score;
            }
        }
    }
}
