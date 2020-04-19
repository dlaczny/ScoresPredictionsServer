using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScoresPredictionsServer.Models;
using ScoresPredictionsServer.Repositories;
using ScoresPredictionsServer.Repositories.Interfaces;

namespace ScoresPredictionsServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private IMongoDatabase mongoDatabase;

        private IJobRepository jobRepository;

        public HomeController(ILogger<HomeController> logger, IOptions<Settings> options, IJobRepository _jobRepository)
        {
            _logger = logger;
            var client = new MongoClient(options.Value.ConnectionString);
            mongoDatabase = client.GetDatabase(options.Value.Database);
            jobRepository = _jobRepository;
        }

        public IActionResult Index()
        {
            //Scraper scraper = new Scraper(mongoDatabase);
            //scraper.Test();

            //jobRepository.UpdateTeams();

            //jobRepository.UpdateIncommingEncounters();

            //jobRepository.AddTournaments();

            jobRepository.UpdateScoresJob(mongoDatabase.GetCollection<Tournament>("Tournaments").AsQueryable().First());

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}