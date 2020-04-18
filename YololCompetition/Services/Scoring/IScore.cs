using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Scoring
{
    public interface IScore
    {
        uint Score(ChallengeDifficulty difficulty, long maxIters, int runtime, int codeChars);
    }
}
