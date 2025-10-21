using ChatAppMongo.Models;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace ChatAppMongo.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IMongoCollection<ChatMessage> _chatCollection;
        private readonly IMongoCollection<ChatGroup> _groupCollection;

        // Tracks connected users and their connection IDs
        private static readonly Dictionary<string, string> UserConnections = new();

        public ChatHub(IMongoDatabase database)
        {
            _chatCollection = database.GetCollection<ChatMessage>("ChatMessages");
            _groupCollection = database.GetCollection<ChatGroup>("ChatGroups");
        }

        #region Connection Management
        public override Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext()?.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                // Track user connection
                UserConnections[userId] = Context.ConnectionId;
            }
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.GetHttpContext()?.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId) && UserConnections.ContainsKey(userId))
            {
                UserConnections.Remove(userId);
            }
            return base.OnDisconnectedAsync(exception);
        }
        #endregion

        #region 1-to-1 Chat
        public async Task JoinRoom(string user1, string user2)
        {
            var roomId = GetRoomId(user1, user2);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        public async Task SendMessage(string senderId, string receiverId, string message)
        {
            var roomId = GetRoomId(senderId, receiverId);

            var chat = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                RoomId = roomId,
                IsRead = false,
                VisibleTo = new List<string> { senderId, receiverId }
            };

            await _chatCollection.InsertOneAsync(chat);

            // Send message to both users in the room
            await Clients.Group(roomId).SendAsync("ReceiveMessage",
                senderId, message, chat.Id, chat.Timestamp);
        }

        public async Task ClearChat(string userId, string friendId)
        {
            var roomId = GetRoomId(userId, friendId);

            // Remove current user from VisibleTo list
            var filter = Builders<ChatMessage>.Filter.And(
                Builders<ChatMessage>.Filter.Eq(m => m.RoomId, roomId),
                Builders<ChatMessage>.Filter.AnyEq(m => m.VisibleTo, userId)
            );
            var update = Builders<ChatMessage>.Update.Pull(m => m.VisibleTo, userId);

            await _chatCollection.UpdateManyAsync(filter, update);

            // Notify only the current user to clear UI
            await Clients.Caller.SendAsync("ClearChatClient");
        }

        private string GetRoomId(string user1, string user2)
        {
            return string.CompareOrdinal(user1, user2) < 0
                ? $"{user1}_{user2}"
                : $"{user2}_{user1}";
        }
        #endregion

        #region Group Chat
        public async Task SendGroupMessage(
            string senderId,
            string groupId,
            string message,
            string? fileUrl = null,
            string? fileType = null,
            string? fileName = null)
        {
            var group = await _groupCollection.Find(g => g.Id == groupId).FirstOrDefaultAsync();
            if (group == null) return;

            var chat = new ChatMessage
            {
                SenderId = senderId,
                GroupId = groupId,
                Message = message,
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = fileName,
                Timestamp = DateTime.UtcNow,
                VisibleTo = group.Members
            };

            await _chatCollection.InsertOneAsync(chat);

            foreach (var memberId in group.Members)
            {
                if (UserConnections.TryGetValue(memberId, out var connId))
                {
                    await Clients.Client(connId).SendAsync("ReceiveGroupMessage",
                        senderId, groupId, message, chat.Id, chat.Timestamp, fileUrl, fileType, fileName);
                }
            }
        }
        #endregion

        #region Delete Message
        public async Task DeleteMessage(string messageId)
        {
            var filter = Builders<ChatMessage>.Filter.Eq(m => m.Id, messageId);
            await _chatCollection.DeleteOneAsync(filter);

            await Clients.All.SendAsync("DeleteMessageClient", messageId);
        }
        #endregion
    }
}
