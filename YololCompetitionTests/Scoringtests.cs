using Microsoft.VisualStudio.TestTools.UnitTesting;
using YololCompetition.Services.Scoring;

namespace YololCompetitionTests
{
    [TestClass]
    public class ScoringTests
    {
        [TestMethod]
        public void Scoring()
        {
            const int tests = 200;
            const int iters = 100;

            var s = new BasicScoring();
            var score = s.Score(tests, 10 * tests, 0);
            Assert.AreEqual(50*1000+1400 - 500u, score);
        }
    }
}
