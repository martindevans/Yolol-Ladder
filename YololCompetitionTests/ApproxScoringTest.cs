using Microsoft.VisualStudio.TestTools.UnitTesting;
using YololCompetition.Extensions;

namespace YololCompetitionTests
{
    [TestClass]
    public class ApproxScoringTest
    {
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
        }
    }
}
