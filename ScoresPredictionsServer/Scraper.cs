using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScoresPredictionsServer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ScoresPredictionsServer
{
    public class Scraper
    {
        private IMongoDatabase mongoDatabase;

        public Scraper(IMongoDatabase _mongoDatabase)
        {
            mongoDatabase = _mongoDatabase;
        }

        public void Test()
        {
            //GetTeamIdOfUrl("abc");

            List<string> scoreboardsForEachWeek = new List<string>();

            List<string> movie1 = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(@"C:\Users\laczn\OneDrive\Pulpit\json\nowe.json"));

            List<string> vs = new List<string>() { "https://lol.gamepedia.com/LEC/2020_Season/Spring_Season/Scoreboards" };

            scoreboardsForEachWeek = ScrapeScoreboardsLinks(@"https://lol.gamepedia.com/LEC/2020_Season/Spring_Season/Scoreboards").Result;

            ScrapeScores(scoreboardsForEachWeek, "https://lol.gamepedia.com/LEC/2020_Season/Spring_Season/Scoreboards");
        }

        private async void ScrapeScores(List<string> weeks, string tournament)
        {
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

                        //mongoDatabase.GetCollection<Match>("Matchs").InsertOne(match);

                        encounter.Matches.Add(match);
                        matches.Add(match);

                        //node.Count();

                        ////var team1Score = terki[1].Descendants("th").ToList()[0].InnerText;
                        ////var team2Score = terki[1].Descendants("th").ToList()[2].InnerText;

                        //if (node[2].Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("sb-header-vertict")).First().InnerText == "Victory")
                        //{
                        //    //team1Win
                        //}
                        //else
                        //{
                        //    //team2Win
                        //}

                        //List<string> team1Players = new List<string>();
                        //List<string> team2Players = new List<string>();

                        //var players = terki[4].Descendants("div").Where(node => node.GetAttributeValue("class", "").Equals("sb-p-name")).ToList();

                        //for (int i = 0; i < players.Count; i++)
                        //{
                        //    if (i < 5)
                        //    {
                        //        team1Players.Add(players[i].Descendants("a").First().Attributes["href"].Value);
                        //    }
                        //    else
                        //    {
                        //        team2Players.Add(players[i].Descendants("a").First().Attributes["href"].Value);
                        //    }
                        //}

                        //var date = node[5].Descendants("span").Where(node => node.GetAttributeValue("class", "").Equals("DateInLocal")).First().InnerText;

                        //2020,1,24,17,40
                        //DateTime ad = DateTime.ParseExact(date, "yyyy,M,dd,HH,mm", CultureInfo.InvariantCulture);
                    }

                    if (matches.Where(x => x.WinningTeamId == encounter.Team1Id).Count() > matches.Where(x => x.WinningTeamId == encounter.Team2Id).Count())
                    {
                        encounter.WinningTeamId = encounter.Team1Id;
                    }
                    else
                    {
                        encounter.WinningTeamId = encounter.Team2Id;
                    }

                    if (!mongoDatabase.GetCollection<Encounter>("Encounters").AsQueryable().Any(x => x.Team1Id == encounter.Team1Id && x.Team2Id == encounter.Team2Id && x.Date == encounter.Date && x.WinningTeamId != null))
                    {
                        mongoDatabase.GetCollection<Encounter>("Encounters").InsertOne(encounter);
                    }
                }
            }
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

        private async Task<List<string>> ScrapeScoreboardsLinks(string tournament)
        {
            var url = tournament;

            var http = new HttpClient();
            var html = await http.GetStringAsync(url);
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

        private async void ScrapeTeams(List<string> tournaments)
        {
            ////List<Team> teams = new List<Team>();
            //foreach (var tournament in tournaments)
            //{
            //    var url = "https://lol.gamepedia.com/" + tournament + "/2019_Season/Summer_Season";

            //    var http = new HttpClient();
            //    var html = await http.GetStringAsync(url);
            //    var htmlDocument = new HtmlDocument();
            //    htmlDocument.LoadHtml(html);

            //    var table = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "").Contains("tournament-rosters maxteams")).
            //    First().Descendants("tbody").ToList();

            //    foreach (var item in table)
            //    {
            //        Team team = new Team();
            //        var link = new HtmlDocument();
            //        link.LoadHtml(item.InnerHtml);

            //        string teamName = link.DocumentNode.Descendants("a").First().Attributes["href"].Value;

            //        var teamUrl = @"https://lol.gamepedia.com" + teamName;

            //        //var img = link.DocumentNode.Descendants("td").First(node => node.GetAttributeValue("class", "").Equals("tournament-roster-logo-cell")).
            //        //    Descendants("img").First().Attributes["src"].Value;

            //        //using (WebClient webClient = new WebClient())
            //        //{
            //        //    webClient.DownloadFile(img, @"C:\Users\laczn\Desktop\testt\" + teamName.Trim('/') + ".png");
            //        //}

            //        //Console.WriteLine(teamUrl);

            //        var link2 = new HtmlDocument();
            //        html = await http.GetStringAsync(teamUrl);
            //        link2.LoadHtml(html);

            //        team.Name = teamName.Trim('/');
            //        team.League = tournament;
            //        team.Players = new List<string>();
            //        //team.ID = teamsList.Count();

            //        try
            //        {
            //            var table2 = link2.DocumentNode.Descendants("table").Where(node => node.GetAttributeValue("class", "").Equals("wikitable")).First().Descendants("tr").ToList();

            //            foreach (var player in table2.Skip(1))
            //            {
            //                var xxx = player.Descendants("td").ToList();

            //                //string cos = xxx[2].InnerText.Trim().Replace("\n", "");
            //                team.Players.Add(xxx[2].InnerText.Trim().Replace("\n", ""));
            //                //Console.WriteLine(xxx[2].InnerText + " " + xxx[4].InnerText);
            //            }

            //            //team.Game = _gameRepository.GetGameID(game);

            //            mongoDatabase.GetCollection<Team>("Teams").InsertOne(team);
            //            //teamsList.Add(team);
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine(ex);
            //        }
            //    }
        }

        //Console.ReadLine();
        //Final();
        //}
    }
}