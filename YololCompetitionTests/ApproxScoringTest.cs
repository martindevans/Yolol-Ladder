using F23.StringSimilarity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YololCompetition.Extensions;
using YololCompetition.Services.Scoring;
using YololCompetition.Services.Verification;

namespace YololCompetitionTests
{
    [TestClass]
    public class ApproxScoringTest
    {
        [TestMethod]
        public void ScoreTest()
        {
            var scorer = new BasicScoring();
            var score = scorer.FinalizeScore(1000, 1000 * 2000, 1400);
            Assert.AreEqual(-100000, score);
        }

        //[TestMethod]
        //public void ApproxScore()
        //{
        //    var executor = new YololInterpretExecutor();

        //    uint Score(int answer)
        //    {
        //        var scorer = new ApproximateScoring();
        //        var es = executor.Prepare(Parser.ParseProgram($":a={answer} done=1").Ok, "done");
        //        es.Run(10, TimeSpan.MaxValue).Wait();
        //        scorer.CheckCase(new Dictionary<string, Value>(), new Dictionary<string, Value> { { "a", (Value)0 } }, es);
        //        return scorer.FinalizeScore(1, 1, 1);
        //    }

        //    var a = Score(0);
        //    var b = Score(10);
        //    var c = Score(100);
        //    var d = Score(500);
        //    var e = Score(750);
        //    var f = Score(1000);

        //    Assert.IsTrue(a > b);
        //    Assert.IsTrue(b > c);
        //    Assert.IsTrue(c > d);
        //    Assert.IsTrue(d > e);
        //    Assert.IsTrue(e > f);

        //    Console.WriteLine($"{a} {b} {c} {d} {e} {f}");
        //}

        [TestMethod]
        public void Levenshtein()
        {
            Assert.AreEqual(3, (int)"abc".Levenshtein("123"));
            Assert.AreEqual(2, (int)"abc".Levenshtein("12c"));
            Assert.AreEqual(3, (int)"abc".Levenshtein("123abc"));
            Assert.AreEqual(2, (int)"abc".Levenshtein("acb"));
        }

        [TestMethod]
        public void Damerau()
        {
            var d = new Damerau();
            Assert.AreEqual(3, d.Distance("abc", "123"));
            Assert.AreEqual(2, d.Distance("abc", "12c"));
            Assert.AreEqual(3, d.Distance("abc", "123abc"));
            Assert.AreEqual(1, d.Distance("abc", "acb"));
        }
    }
}
