using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatAppMongo.Models
{
    public class FriendRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        public string SenderId { get; set; } = null!;
        public string ReceiverId { get; set; } = null!;
        public string Status { get; set; } = null!; // Pending, Accepted
    }
}
