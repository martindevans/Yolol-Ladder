using Microsoft.VisualStudio.TestTools.UnitTesting;
using YololCompetition.Services.Challenge;
using YololCompetition.Services.Scoring;

namespace YololCompetitionTests
{
    [TestClass]
    public class ScoringTests
    {
        [TestMethod]
        public void ScoringZero()
        {
            const int tests = 200;
            const int iters = 100;

            var s = new BasicScoring();
            var score = s.Score(ChallengeDifficulty.Easy, tests * iters, tests * iters, 1400);
            Assert.AreEqual(0u, score);
        }
    }
}
