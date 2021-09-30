using System;
using System.Threading.Tasks;

namespace YololCompetition.Services.Schedule
{
    public enum SchedulerState
    {
        StartingChallenge,
        WaitingNoChallengesInPool,
        WaitingChallengeEnd,
        EndingChallenge,
        WaitingCooldown
    }

    public interface IScheduler
    {
        public Task Start();

        public Task Poke();

        SchedulerState State { get; }

        public DateTime? EndTime { get; }
    }
}
