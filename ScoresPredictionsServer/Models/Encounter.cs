using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Models
{
    public class Encounter
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement]
        public DateTime Date { get; set; }

        [BsonElement]
        public string Team1Id { get; set; }

        [BsonElement]
        public string Team2Id { get; set; }

        [BsonElement]
        public string WinningTeamId { get; set; }

        [BsonElement]
        public string WinningTeamPredictionId { get; set; }

        [BsonElement]
        public string Tournament { get; set; }

        [BsonElement]
        public string Format { get; set; }

        [BsonElement]
        public List<string> Matches { get; set; }
    }
}