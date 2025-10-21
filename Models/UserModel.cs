using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace ChatAppMongo.Models
{
    public class UserModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool IsOnline { get; set; } = false;

        public List<string> Friends { get; set; } = new List<string>();

        // Make Email and PhotoUrl nullable
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
