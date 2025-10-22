using ChatAppMongo.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatAppMongo.Controllers
{
    public class ChatController : Controller
    {
        private readonly IMongoCollection<UserModel> _users;
        private readonly IMongoCollection<ChatMessage> _messages;
        private readonly IMongoCollection<FriendRequest> _requests;
        private readonly IMongoCollection<ChatMessage> _chatCollection;

        public ChatController(IMongoDatabase db)
        {
            _users = db.GetCollection<UserModel>("Users");
            _messages = db.GetCollection<ChatMessage>("ChatMessages");
            _requests = db.GetCollection<FriendRequest>("FriendRequests");
            _chatCollection = db.GetCollection<ChatMessage>("ChatMessages");
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Friends()
        {
            var currentUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUserId))
                return RedirectToAction("Login", "Account");

            var allUsers = await _users.Find(u => u.Id != currentUserId).ToListAsync();

            var pendingRequests = await _requests
                .Find(r => r.ReceiverId == currentUserId && r.Status == "Pending")
                .ToListAsync();
            var pendingRequestIds = pendingRequests.Select(r => r.SenderId).ToList();
            var pendingUsers = allUsers.Where(u => pendingRequestIds.Contains(u.Id)).ToList();

            var currentUser = await _users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
            var friendIds = currentUser?.Friends ?? new List<string>();
            var friendsList = allUsers.Where(u => friendIds.Contains(u.Id)).ToList();

            var model = new Tuple<IEnumerable<UserModel>, IEnumerable<UserModel>>(pendingUsers, friendsList);
            return View(model);
        }

        public async Task<IActionResult> ChatRoom(string friendId)
        {
            var currentUser = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(friendId))
                return RedirectToAction("Login", "Account");

            var friend = await _users.Find(u => u.Id == friendId).FirstOrDefaultAsync();
            var friendUsername = friend?.Username ?? "Friend";

            var roomId = GetRoomId(currentUser, friendId);

            var messages = await _messages
                .Find(m => m.RoomId == roomId)
                .SortBy(m => m.Timestamp)
                .ToListAsync();

            ViewBag.FriendId = friendId;
            ViewBag.FriendUsername = friendUsername;
            return View(messages);
        }

        private string GetRoomId(string user1, string user2)
        {
            return string.CompareOrdinal(user1, user2) < 0
                ? $"{user1}_{user2}"
                : $"{user2}_{user1}";
        }

        //----------------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Chat(string friendId)
        {
            var currentUser = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(friendId))
                return RedirectToAction("Friends");

            var messages = await _chatCollection
                .Find(m => (m.SenderId == currentUser && m.ReceiverId == friendId) ||
                            (m.SenderId == friendId && m.ReceiverId == currentUser))
                .SortBy(m => m.Timestamp)
                .ToListAsync();

            ViewBag.FriendId = friendId;
            ViewBag.FriendUsername = "Friend"; // optionally load friend's username
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string senderId, string receiverId, string message)
        {
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(message))
                return Json(new { success = false });

            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Message = message
            };

            await _chatCollection.InsertOneAsync(chatMessage);

            // Optionally broadcast via SignalR here
            return Json(new
            {
                success = true,
                messageId = chatMessage.Id,
                timestamp = chatMessage.Timestamp
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string currentUser, string friendId)
        {
            var messages = await _chatCollection
                .Find(m => (m.SenderId == currentUser && m.ReceiverId == friendId) ||
                            (m.SenderId == friendId && m.ReceiverId == currentUser))
                .SortBy(m => m.Timestamp)
                .ToListAsync();

            var result = messages.Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                message = m.Message,
                timestamp = m.Timestamp
            });

            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetUnreadCounts(string currentUserId)
        {
            var counts = await _chatCollection.Aggregate()
                .Match(m => m.ReceiverId == currentUserId && !m.IsRead)
                .Group(m => m.SenderId, g => new { SenderId = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(counts);
        }
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(string currentUserId, string friendId)
        {
            var filter = Builders<ChatMessage>.Filter.Where(m => m.ReceiverId == currentUserId && m.SenderId == friendId && !m.IsRead);
            var update = Builders<ChatMessage>.Update.Set(m => m.IsRead, true);

            await _chatCollection.UpdateManyAsync(filter, update);
            return Json(new { success = true });
        }

    }
}
