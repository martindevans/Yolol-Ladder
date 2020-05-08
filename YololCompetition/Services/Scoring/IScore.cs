namespace YololCompetition.Services.Scoring
{
    public interface IScore
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="totalTests">Total number of test cases</param>
        /// <param name="totalTicks">Total ticks used</param>
        /// <param name="codeChars">Total number of characters used</param>
        /// <returns></returns>
        uint Score(uint totalTests, uint totalTicks, int codeChars);
    }
}
