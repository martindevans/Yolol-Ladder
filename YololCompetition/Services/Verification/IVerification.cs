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

        public string? Hint { get; }

        public Success(uint score, uint iterations, uint length, string? hint)
        {
            Score = score;
            Iterations = iterations;
            Length = length;
            Hint = hint;
        }
    }

    public class Failure
    {
        public FailureType Type { get; }

        public string? Hint { get; }

        public Failure(FailureType type, string? hint)
        {
            Type = type;
            Hint = hint;
        }
    }

    public enum FailureType
    {
        Other,

        ParseFailed,
        RuntimeTooLong,
        IncorrectResult,
        ProgramTooLarge,
        InvalidProgramForChipType,
    }
}
