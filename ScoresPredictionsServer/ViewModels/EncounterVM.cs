using ScoresPredictionsServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.ViewModels
{
    public class EncounterVM
    {
        public IEnumerable<Encounter> Encounters;
        public IEnumerable<Team> Teams;
    }
}