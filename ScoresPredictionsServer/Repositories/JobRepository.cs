using Hangfire;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using ScoresPredictionsServer.Models;
using ScoresPredictionsServer.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        public async void UpdateIncommingEncounters(Tournament tournament)
        {
            //var url = "https://lol.gamepedia.com/LEC/2020_Season/Spring_Playoffs";

            var http = new HttpClient();
            var html = await http.GetStringAsync(tournament.TournamentUrl);
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

                    newEncounter.Tournament = tournament.TournamentUrl;

                    if (!mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().Any(x => x.Team1Id == newEncounter.Team1Id && x.Team2Id == newEncounter.Team2Id && x.Date == newEncounter.Date))
                    {
                        newEncounter = CanculatePredyction(newEncounter);
                        await mongoDatabase.GetCollection<Encounter>("Encounters").InsertOneAsync(newEncounter);
                    }
                    else
                    {
                        if (mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().First(x => x.Team1Id == newEncounter.Team1Id && x.Team2Id == newEncounter.Team2Id && x.Date == newEncounter.Date).WinningTeamId == null)
                        {
                            newEncounter = CanculatePredyction(newEncounter);
                            var encounterId = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable<Encounter>().First(x => x.Team1Id == newEncounter.Team1Id && x.Team2Id == newEncounter.Team2Id && x.Date == newEncounter.Date);
                            var filter = Builders<Encounter>.Filter.Eq("_id", encounterId);

                            await mongoDatabase.GetCollection<Encounter>("Encounters").ReplaceOneAsync(filter, newEncounter);
                        }
                    }
                }
            }
        }

        public void UpdateScoresJob(Tournament tournament)
        {
            //RecurringJob.AddOrUpdate(tournament.ID.ToString(),
            //      () => UpdateScores(tournament),
            //      Cron.Daily, TimeZoneInfo.Local);

            //RecurringJob.AddOrUpdate("test3",
            //      () => UpdateScores(tournament.TournamentUrl),
            //      Cron.Daily, TimeZoneInfo.Local);

            var DB = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable().ToList();

            RecurringJob.AddOrUpdate("test3",
                  () => UpdateScores(@"https://lol.gamepedia.com/LEC/2020_Season/Spring_Season/Scoreboards"),
                  Cron.Daily, TimeZoneInfo.Local);
        }

        public void UpdateScores(string tournamentUrl)
        {
            var DB = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable().ToList();

            ScrapeScores(tournamentUrl);
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

        public async void AddTournaments()
        {
            List<string> tournamentsLinks = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(@"C:\Users\laczn\OneDrive\Pulpit\json\nowe.json"));

            foreach (var tournamentsLink in tournamentsLinks)
            {
                Tournament tournament = new Tournament();

                var http = new HttpClient();
                var html = await http.GetStringAsync(tournamentsLink);
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                tournament.Region = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("region-icon")).First().InnerText;
                tournament.TournamentUrl = tournamentsLink;
                tournament.Enabled = true;

                var test = htmlDocument.DocumentNode.Descendants("table").Where(node => node.GetAttributeValue("id", "").Equals("infoboxTournament")).First();

                try
                {
                    var startDateString = test.Descendants("td").Where(x => x.InnerText == "Start Date").First().NextSibling.InnerText;
                    var endDateString = test.Descendants("td").Where(x => x.InnerText == "End Date").First().NextSibling.InnerText;
                    tournament.StartDate = DateTime.ParseExact(startDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    tournament.EndDate = DateTime.ParseExact(endDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    try
                    {
                        var date = test.Descendants("td").Where(x => x.InnerText == "Date").First().NextSibling.InnerText;
                        tournament.StartDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        tournament.EndDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    catch (Exception exx)
                    {
                    }
                }

                mongoDatabase.GetCollection<Tournament>("Tournaments").InsertOne(tournament);
            }
        }

        private async Task ScrapeScores(string tournament)
        {
            List<string> weeks = new List<string>();
            weeks = ScrapeScoreboardsLinks(tournament);
            foreach (var week in weeks)
            {
                var http = new HttpClient();
                var html = await http.GetStringAsync(week);
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                //var table = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("mw-parser-output")).ElementAt(1).Descendants("table").Where(node => node.GetAttributeValue("class", "").Equals("sb"));

                var content = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("mw-parser-output")).ElementAt(1).Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("inline-content"));

                foreach (var itemc in content)
                {
                    Encounter encounter = new Encounter();

                    var tables = itemc.Descendants("table").Where(node => node.GetAttributeValue("class", "").Equals("sb"));

                    //Look for teamId or create new team
                    encounter.Team1Id = GetTeamIdOfUrl(tables.First().Descendants("a").ToList()[0].Attributes["href"].Value);
                    encounter.Team2Id = GetTeamIdOfUrl(tables.First().Descendants("a").ToList()[2].Attributes["href"].Value);

                    encounter.Tournament = tournament;

                    var date = tables.First().Descendants("span").Where(node => node.GetAttributeValue("class", "").Equals("DateInLocal")).First().InnerText;
                    encounter.Date = DateTime.ParseExact(date, "yyyy,M,dd,HH,mm", CultureInfo.InvariantCulture);
                    encounter.Matches = new List<Match>();

                    if (tables.Count() == 1)
                    {
                        encounter.Format = "BO1";
                    }
                    else if (tables.Count() == 2 || tables.Count() == 3)
                    {
                        encounter.Format = "BO3/BO5";
                    }
                    else if (tables.Count() > 3)
                    {
                        encounter.Format = "BO5";
                    }
                    List<Match> matches = new List<Match>();
                    foreach (var item in tables)
                    {
                        Match match = new Match();

                        var node = item.Descendants("tr").ToList();

                        FillMatch(match, node);

                        encounter.Matches.Add(match);
                        matches.Add(match);
                    }

                    if (matches.Where(x => x.WinningTeamId == encounter.Team1Id).Count() > matches.Where(x => x.WinningTeamId == encounter.Team2Id).Count())
                    {
                        encounter.WinningTeamId = encounter.Team1Id;
                    }
                    else
                    {
                        encounter.WinningTeamId = encounter.Team2Id;
                    }

                    var DB = mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable().ToList();

                    if (!DB.Any(x => x.Team1Id == encounter.Team1Id && x.Team2Id == encounter.Team2Id && x.Date == encounter.Date && x.WinningTeamId != null))
                    {
                        mongoDatabase.GetCollection<Encounter>("Encounters").InsertOne(encounter);
                    }
                }
            }
        }

        private List<string> ScrapeScoreboardsLinks(string tournament)
        {
            var url = tournament;

            var http = new HttpClient();
            var html = http.GetStringAsync(url).Result;
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var table = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "").Contains("tabheader-top"));

            var urls = table.ElementAt(2).Descendants("a");

            List<string> scoreboardsForEachWeek = new List<string>();
            scoreboardsForEachWeek.Add(url);
            foreach (var item in urls.Skip(1))
            {
                scoreboardsForEachWeek.Add(@"https://lol.gamepedia.com" + item.Attributes["href"].Value);
            }
            return scoreboardsForEachWeek;
        }

        private string GetTeamIdOfUrl(string url)
        {
            Team team = new Team();
            team.Url = url;

            team = ScrapeTeamDetails(team).Result;

            if (CollectionExistsAsync("Teams").Result)
            {
                if (mongoDatabase.GetCollection<Team>("Teams").FindAsync(x => x.Url.Equals(url)).Result.Any())
                {
                    return mongoDatabase.GetCollection<Team>("Teams").FindAsync(x => x.Url.Equals(url)).Result.First().ID.ToString();
                }
                else
                {
                    mongoDatabase.GetCollection<Team>("Teams").InsertOne(team);
                    return mongoDatabase.GetCollection<Team>("Teams").FindAsync(x => x.Url.Equals(url)).Result.First().ID.ToString();
                }
            }
            else
            {
                mongoDatabase.GetCollection<Team>("Teams").InsertOne(team);
                return mongoDatabase.GetCollection<Team>("Teams").FindAsync(x => x.Url.Equals(url)).Result.First().ID.ToString();
            }
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            //filter by collection name
            var collections = await GetDatabase().ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            //check for existence
            return await collections.AnyAsync();
        }

        private IMongoDatabase GetDatabase()
        {
            return mongoDatabase;
        }

        private async Task<Team> ScrapeTeamDetails(Team team)
        {
            var url = "https://lol.gamepedia.com" + team.Url;

            team.NameFromUrl = team.Url.Trim('/').Replace("_", " ");

            var http = new HttpClient();
            var html = await http.GetStringAsync(url);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var name = htmlDocument.DocumentNode.Descendants("th").Where(node => node.GetAttributeValue("class", "").Equals("infobox-title")).ToList().First().InnerText;

            team.Name = name;

            var http2 = new HttpClient();
            var html2 = await http2.GetStringAsync(url);
            var htmlDocument2 = new HtmlDocument();
            htmlDocument.LoadHtml(html2);

            var img = htmlDocument.DocumentNode.Descendants("a").Where(node => node.GetAttributeValue("class", "").Equals("image")).ToList().First().Descendants("img").First().Attributes["src"].Value;

            team.LogoUrl = img;

            return team;
        }

        private void FillMatch(Match match, List<HtmlNode> node)
        {
            if (node[2].Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("sb-header-vertict")).First().InnerText == "Victory")
            {
                List<Team> teams = mongoDatabase.GetCollection<Team>("Teams").AsQueryable().ToList();

                match.WinningTeamId = node[0].Descendants("a").ToList()[0].Attributes["href"].Value;
                match.WinningTeamId = teams.First(x => x.Url == match.WinningTeamId).ID.ToString();
                match.LoosingTeamId = node[0].Descendants("a").ToList()[2].Attributes["href"].Value;
                match.LoosingTeamId = teams.First(x => x.Url == match.LoosingTeamId).ID.ToString();

                match.WinningTeamPlayers = new List<string>();
                match.LoosingTeamPlayers = new List<string>();

                var players = node[4].Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("sb-p-name")).ToList();

                for (int i = 0; i < players.Count; i++)
                {
                    if (i < 5)
                    {
                        match.WinningTeamPlayers.Add(players[i].Descendants("a").First().Attributes["href"].Value);
                    }
                    else
                    {
                        match.LoosingTeamPlayers.Add(players[i].Descendants("a").First().Attributes["href"].Value);
                    }
                }
            }
            else
            {
                match.WinningTeamId = node[0].Descendants("a").ToList()[2].Attributes["href"].Value;
                match.LoosingTeamId = node[0].Descendants("a").ToList()[0].Attributes["href"].Value;
            }
        }

        public async void TryTest()
        {
            var url = "https://lol.gamepedia.com/LEC/2020_Season/Spring_Playoffs";
            var http = new HttpClient();
            var html = await http.GetStringAsync(url);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var test = htmlDocument.DocumentNode.Descendants("table").Where(node => node.GetAttributeValue("id", "").Equals("infoboxTournament")).First();

            //newEncounter.Date = DateTime.ParseExact(date, "yyyy,M,dd,HH,mm", CultureInfo.InvariantCulture);

            var startDateString = test.Descendants("td").Where(x => x.InnerText == "Start Date").First().NextSibling.InnerText;
            var endDateString = test.Descendants("td").Where(x => x.InnerText == "End Date").First().NextSibling.InnerText;
        }
    }
}