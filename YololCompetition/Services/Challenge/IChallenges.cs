using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
namespace YololCompetition.Services.Challenge
{
    public interface IChallenges
    {
        Task Create(Challenge challenge);

        Task<long> GetPendingCount();

        Task<Challenge?> GetCurrentChallenge();

        Task<Challenge?> StartNext();

        Task EndCurrentChallenge();
    }
}
