using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Scoring
{
    public class BasicScoring
        : IScore
    {
        private const int MaxChars = 20 * 70;

        public uint Score(ChallengeDifficulty difficulty, long maxIters, int runtime, int codeChars)
        {
            var itersSpare = maxIters - runtime;
            var charsSpare = MaxChars - codeChars;

            var score = ((double)itersSpare / maxIters) * 100 + ((double)charsSpare / MaxChars) * 100;

            return (uint)(score * (int)difficulty);
        }
    }
}
