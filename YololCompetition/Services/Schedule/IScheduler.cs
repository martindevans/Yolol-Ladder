using System.Threading.Tasks;

namespace YololCompetition.Services.Schedule
{
    public interface IScheduler
    {
        public Task Start();

        public Task Poke();
    }
}
