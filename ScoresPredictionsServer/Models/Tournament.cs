using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Models
{
    public class Tournament
    {
        [BsonId]
        public ObjectId ID { get; set; }

        [BsonElement]
        public string TournamentUrl { get; set; }

        [BsonElement]
        public bool Enabled { get; set; }

        [BsonElement]
        public DateTime StartDate { get; set; }

        [BsonElement]
        public DateTime EndDate { get; set; }

        [BsonElement]
        public string Region { get; set; }
    }
}