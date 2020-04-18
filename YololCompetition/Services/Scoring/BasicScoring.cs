using System;
using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Scoring
{
    public class BasicScoring
        : IScore
    {
        private const int MaxChars = 20 * 70;

        public uint Score(ChallengeDifficulty difficulty, long maxIters, int runtime, int codeChars)
        {
            checked
            {
                var itersSpare = (double)Math.Max(0, maxIters - runtime);
                var charsSpare = (double)Math.Max(0, MaxChars - codeChars);

                var score = itersSpare / maxIters * 200
                            + charsSpare / MaxChars * 100;

                return (uint)(score * (int)difficulty);
            }
        }
    }
}
