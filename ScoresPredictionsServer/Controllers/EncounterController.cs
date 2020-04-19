using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScoresPredictionsServer.Models;
using ScoresPredictionsServer.ViewModels;

namespace ScoresPredictionsServer.Controllers
{
    public class EncounterController : Controller
    {
        private IMongoDatabase mongoDatabase;

        public EncounterController(IOptions<Settings> options)
        {
            var client = new MongoClient(options.Value.ConnectionString);
            mongoDatabase = client.GetDatabase(options.Value.Database);
        }

        // GET: Encounter
        public ActionResult Index()
        {
            //mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().ToList();
            //return View(mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().ToList().AsEnumerable());

            return View(new EncounterVM()
            {
                Encounters = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().AsEnumerable(),
                Teams = mongoDatabase.GetCollection<Team>("Teams").AsQueryable<Team>().ToList().AsEnumerable()
            });
        }

        // GET: Encounter/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: Encounter/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Encounter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: Encounter/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: Encounter/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: Encounter/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: Encounter/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}