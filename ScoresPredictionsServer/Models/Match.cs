using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Models
{
    public class Match
    {
        //[BsonId]
        //public ObjectId Id { get; set; }

        [BsonElement]
        public string WinningTeamId { get; set; }

        [BsonElement]
        public string LoosingTeamId { get; set; }

        [BsonElement]
        public List<string> WinningTeamPlayers { get; set; }

        [BsonElement]
        public List<string> LoosingTeamPlayers { get; set; }
    }
}