using Hangfire;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ScoresPredictionsServer.Models;
using ScoresPredictionsServer.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Repositories
{
    public class JobRepository : IJobRepository
    {
        private IMongoDatabase mongoDatabase;

        public JobRepository(IOptions<Settings> options)
        {
            var client = new MongoClient(options.Value.ConnectionString);
            mongoDatabase = client.GetDatabase(options.Value.Database);
        }

        public void UpdateTeamsJob()
        {
            RecurringJob.AddOrUpdate(
                () => UpdateTeams(),
                "0 2 * * 1");
        }

        public async void UpdateTeams()
        {
            var teams = mongoDatabase.GetCollection<Team>("Teams").AsQueryable<Team>();

            //mongoDatabase.GetCollection<Team>("Teams").UpdateOne();

            foreach (var team in teams)
            {
                var url = "https://lol.gamepedia.com" + team.Url;

                var http = new HttpClient();
                var html = await http.GetStringAsync(url);
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                var filter = Builders<Team>.Filter.Eq("_id", team.ID);

                var img = htmlDocument.DocumentNode.Descendants("a").Where(node => node.GetAttributeValue("class", "").Equals("image")).ToList().First().Descendants("img").First().Attributes["src"].Value;

                var update = Builders<Team>.Update.Set("LogoUrl", img);

                mongoDatabase.GetCollection<Team>("Teams").UpdateOne(filter, update);
            }
        }

        public void UpdateIncommingEncountersJob()
        {
            RecurringJob.AddOrUpdate(
                () => UpdateTeams(),
                "0 2 * * 1");
        }

        public async void UpdateIncommingEncounters()
        {
            var url = "https://lol.gamepedia.com/LEC/2020_Season/Spring_Playoffs";

            var http = new HttpClient();
            var html = await http.GetStringAsync(url);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var content = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("id", "").Equals("matchlist-content-wrapper")).First().Descendants("tbody").ToList();

            foreach (var table in content)
            {
                var encounters = table.Descendants("tr").Where(node => node.GetAttributeValue("class", "").Contains("ml-row")).ToList();

                foreach (var encounter in encounters)
                {
                    Encounter newEncounter = new Encounter();
                    string teams2;
                    string teams1;
                    try
                    {
                        teams2 = encounter.Descendants("td").Where(node => node.GetAttributeValue("class", "").Contains("matchlist-team2")).First().Attributes["data-teamhighlight"].Value;
                        teams1 = encounter.Descendants("td").Where(node => node.GetAttributeValue("class", "").Contains("matchlist-team1")).First().Attributes["data-teamhighlight"].Value;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }

                    newEncounter.Team2Id = mongoDatabase.GetCollection<Team>("Teams").AsQueryable<Team>().First(x => x.NameFromUrl == teams2).ID.ToString();
                    newEncounter.Team1Id = mongoDatabase.GetCollection<Team>("Teams").AsQueryable<Team>().First(x => x.NameFromUrl == teams1).ID.ToString();

                    var date = encounter.Descendants("span").Where(node => node.GetAttributeValue("class", "").Contains("TimeInLocal")).First().InnerText;
                    newEncounter.Date = DateTime.ParseExact(date, "yyyy,M,dd,HH,mm", CultureInfo.InvariantCulture);

                    newEncounter.Tournament = url;

                    newEncounter = CanculatePredyction(newEncounter);

                    if (!mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().Any(x => x.Team1Id == newEncounter.Team1Id && x.Team2Id == newEncounter.Team2Id && x.Date == newEncounter.Date))
                    {
                        await mongoDatabase.GetCollection<Encounter>("Encounters").InsertOneAsync(newEncounter);
                    }
                    else
                    {
                        var encounterId = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().First(x => x.Team1Id == newEncounter.Team1Id && x.Team2Id == newEncounter.Team2Id && x.Date == newEncounter.Date);
                        var filter = Builders<Encounter>.Filter.Eq("_id", encounterId);

                        await mongoDatabase.GetCollection<Encounter>("Encounters").ReplaceOneAsync(filter, newEncounter);
                    }
                }
            }
        }

        private Encounter CanculatePredyction(Encounter encounter)
        {
            List<Encounter> team1AllEncounters = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().Where(x => x.Team1Id == encounter.Team1Id && x.Team2Id != encounter.Team2Id || x.Team2Id == encounter.Team1Id && x.Team1Id != encounter.Team2Id).OrderBy(x => x.Date).ToList();
            var team1winCount = team1AllEncounters.Where(x => x.WinningTeamId == encounter.Team1Id && x.Date.AddMonths(6) > DateTime.Now).Count();
            var team1allCount = team1AllEncounters.Where(x => x.Date.AddMonths(6) > DateTime.Now).Count();
            double team1winPercentage;
            if (team1winCount == 0)
            {
                team1winPercentage = 0;
            }
            else if (team1winCount == team1allCount)
            {
                team1winPercentage = 1;
            }
            else
            {
                team1winPercentage = (double)team1winCount / (double)team1allCount;
            }

            List<Encounter> team2AllEncounters = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().Where(x => x.Team1Id == encounter.Team2Id && x.Team2Id != encounter.Team1Id || x.Team2Id == encounter.Team2Id && x.Team1Id != encounter.Team1Id).OrderBy(x => x.Date).ToList();
            var team2winCount = team2AllEncounters.Where(x => x.WinningTeamId == encounter.Team2Id && x.Date.AddMonths(6) > DateTime.Now).Count();
            var team2allCount = team2AllEncounters.Where(x => x.Date.AddMonths(6) > DateTime.Now).Count();
            double team2winPercentage;
            if (team2winCount == 0)
            {
                team2winPercentage = 0;
            }
            else if (team2winCount == team2allCount)
            {
                team2winPercentage = 1;
            }
            else
            {
                team2winPercentage = (double)team2winCount / (double)team2allCount;
            }

            double team1winPercentageChance;
            double team2winPercentageChance;
            if (team1winPercentage == 0)
            {
                team1winPercentageChance = 0;
                team2winPercentageChance = 1;
            }
            else if (team2winPercentage == 0)
            {
                team1winPercentageChance = 1;
                team2winPercentageChance = 0;
            }
            else
            {
                team1winPercentageChance = (team1winPercentage / team2winPercentage) / 2;
                team2winPercentageChance = 1 - team1winPercentageChance;
            }

            List<Encounter> headToHead = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().Where(x => x.Team1Id == encounter.Team1Id && x.Team2Id == encounter.Team2Id || x.Team2Id == encounter.Team1Id && x.Team1Id == encounter.Team2Id).ToList();
            headToHead = headToHead.Where(x => x.Date.AddMonths(6) > DateTime.Now).ToList();

            double test;
            double test2;
            if (headToHead.Where(x => x.WinningTeamId == encounter.Team1Id).Count() == 2)
            {
                test = 1;
                test2 = 0;
            }
            else if (headToHead.Where(x => x.WinningTeamId == encounter.Team1Id).Count() == 0)
            {
                test = 0;
                test2 = 1;
            }
            else
            {
                test = ((double)headToHead.Where(x => x.WinningTeamId == encounter.Team1Id).Count() / (double)headToHead.Where(x => x.WinningTeamId == encounter.Team2Id).Count()) / 2;
                test2 = 1 - test;
            }

            double team1WinChance = (team1winPercentageChance * 1 + test * 2) / (1 + 2);
            double team2WinChance = (team2winPercentageChance * 1 + test2 * 2) / (1 + 2);

            if (team1WinChance > team2WinChance)
            {
                encounter.WinningTeamPredictionId = encounter.Team1Id;
                encounter.WinningTeamPredictionPercentage = team1WinChance;
            }
            else if (team1WinChance < team2WinChance)
            {
                encounter.WinningTeamPredictionId = encounter.Team2Id;
                encounter.WinningTeamPredictionPercentage = team2WinChance;
            }
            else
            {
                encounter.WinningTeamPredictionId = "DRAW";
                encounter.WinningTeamPredictionPercentage = team1WinChance;
            }

            return encounter;
        }
    }
}