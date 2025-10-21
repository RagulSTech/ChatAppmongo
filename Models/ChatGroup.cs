using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatAppMongo.Models
{
    public class ChatGroup
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!; // MongoDB will auto-generate

        public string Name { get; set; } = string.Empty;
        public string IconUrl { get; set; } = "/images/default-group.png";
        public string CreatedBy { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new List<string>();
        public List<string> PendingRequests { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<GroupMessage> Messages { get; set; } = new();
    }
    public class GroupMessage
    {
        public string SenderId { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
