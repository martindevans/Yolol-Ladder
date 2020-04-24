    using System.Threading.Tasks;

namespace YololCompetition.Services.Verification
{
    public interface IVerification
    {
        Task<(Success?, Failure?)> Verify(Challenge.Challenge challenge, string yolol);
    }

    public class Success
    {
        public uint Score { get; }

        public uint Iterations { get; }

        public uint Length { get; }

        public Success(uint score, uint iterations, uint length)
        {
            Score = score;
            Iterations = iterations;
            Length = length;
        }
    }

    public class Failure
    {
        public FailureType Type { get; }

        public Failure(FailureType type)
        {
            Type = type;
        }
    }

    public enum FailureType
    {
        ParseFailed,
        RuntimeTooLong,
        IncorrectResult,
        ProgramTooLarge
    }
}
