using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScoresPredictionsServer.Models
{
    public class Team
    {
        [BsonId]
        public ObjectId ID { get; set; }

        [BsonElement]
        public string Url { get; set; }
    }
}