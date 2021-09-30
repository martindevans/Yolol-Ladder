namespace YololCompetition.Services.Trueskill
{
    public readonly struct TrueskillRating
    {
        public double Mean { get; }
        public double StdDev { get; }
        public double ConservativeEstimate => Mean - 3 * StdDev;

        public TrueskillRating(double mean, double stdDev)
        {
            Mean = mean;
            StdDev = stdDev;
        }
    }
}
