using ScoresPredictionsServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Repositories.Interfaces
{
    public interface IJobRepository
    {
        void UpdateTeams();

        void UpdateIncommingEncounters(Tournament tournament);

        void UpdateScoresJob(Tournament tournament);

        //
        void AddTournaments();

        void TryTest();
    }
}