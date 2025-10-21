using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatAppMongo.Models
{
    public class ChatMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string RoomId { get; set; }
        public string SenderId { get; set; }
        public string? ReceiverId { get; set; } // null if group message
        public string? GroupId { get; set; }    // group messages
        public string Message { get; set; }
        public string senderUsername { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; } = false;
        public List<string> VisibleTo { get; set; } = new List<string>();

        public string? FileUrl { get; set; }       // URL/path of uploaded file
        public string? FileType { get; set; }      // photo, video, document
        public string? FileName { get; set; }      // original file name
        [BsonIgnore]
        public string FromUserId
        {
            get => SenderId;
            set => SenderId = value;
        }
        [BsonIgnore]
        public string ToUserId
        {
            get => ReceiverId;
            set => ReceiverId = value;
        }
    }
}
