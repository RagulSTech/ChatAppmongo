using ChatAppMongo.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatAppMongo.Controllers
{
    public class FriendController : Controller
    {
        private readonly IMongoCollection<UserModel> _users;
        private readonly IMongoCollection<FriendRequest> _requests;

        public FriendController(IMongoDatabase database)
        {
            _users = database.GetCollection<UserModel>("Users");
            _requests = database.GetCollection<FriendRequest>("FriendRequests");
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            var currentUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(currentUserId))
                return Json(new List<object>());

            var filter = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                Builders<UserModel>.Filter.Ne(u => u.Id, currentUserId)
            );

            var users = await _users.Find(filter).Limit(10).ToListAsync();

            var sentRequests = await _requests
                .Find(r => r.SenderId == currentUserId && r.Status == "Pending")
                .ToListAsync();

            var sentIds = sentRequests.Select(r => r.ReceiverId).ToHashSet();

            return Json(users.Select(u => new
            {
                id = u.Id.ToString(),          // camelCase for JS
                username = u.Username,
                isFriend = u.Friends != null && u.Friends.Contains(currentUserId),
                requestSent = sentIds.Contains(u.Id)
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SendRequest(string receiverId)
        {
            var senderId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                return Json(new { success = false, message = "Invalid request." });

            var receiver = await _users.Find(u => u.Id == receiverId).FirstOrDefaultAsync();
            if (receiver == null)
                return Json(new { success = false, message = "User not found." });

            var existing = await _requests.Find(x =>
                (x.SenderId == senderId && x.ReceiverId == receiverId) ||
                (x.SenderId == receiverId && x.ReceiverId == senderId)
            ).FirstOrDefaultAsync();

            if (existing != null)
                return Json(new { success = false, message = "Request already exists." });

            var req = new FriendRequest
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = "Pending"
            };

            await _requests.InsertOneAsync(req);
            return Json(new { success = true, message = "Request sent successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(string senderId)
        {
            var receiverId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(senderId))
                return Json(new { success = false, message = "Invalid request." });

            var requestFilter = Builders<FriendRequest>.Filter.And(
                Builders<FriendRequest>.Filter.Eq(r => r.SenderId, senderId),
                Builders<FriendRequest>.Filter.Eq(r => r.ReceiverId, receiverId)
            );

            var update = Builders<FriendRequest>.Update.Set(r => r.Status, "Accepted");
            await _requests.UpdateOneAsync(requestFilter, update);

            var sender = await _users.Find(u => u.Id == senderId).FirstOrDefaultAsync();
            var receiver = await _users.Find(u => u.Id == receiverId).FirstOrDefaultAsync();

            if (sender != null && receiver != null)
            {
                sender.Friends ??= new List<string>();
                receiver.Friends ??= new List<string>();

                if (!receiver.Friends.Contains(senderId)) receiver.Friends.Add(senderId);
                if (!sender.Friends.Contains(receiverId)) sender.Friends.Add(receiverId);

                await _users.ReplaceOneAsync(u => u.Id == receiver.Id, receiver);
                await _users.ReplaceOneAsync(u => u.Id == sender.Id, sender);
            }

            // Return minimal friend info in camelCase for JS
            return Json(new
            {
                success = true,
                message = "Friend request accepted!",
                friend = new { id = sender.Id, username = sender.Username }
            });
        }

        [HttpPost]
        public async Task<IActionResult> RejectRequest(string senderId)
        {
            var receiverId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(senderId))
                return Json(new { success = false, message = "Invalid request." });

            await _requests.DeleteOneAsync(
                Builders<FriendRequest>.Filter.And(
                    Builders<FriendRequest>.Filter.Eq(r => r.SenderId, senderId),
                    Builders<FriendRequest>.Filter.Eq(r => r.ReceiverId, receiverId)
                )
            );

            return Json(new { success = true, message = "Friend request rejected!" });
        }

        [HttpGet]
        public async Task<IActionResult> FriendRequests()
        {
            var currentUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUserId))
                return RedirectToAction("Login", "Account");

            var pendingRequests = await _requests
                .Find(r => r.ReceiverId == currentUserId && r.Status == "Pending")
                .ToListAsync();

            var senderIds = pendingRequests.Select(r => r.SenderId).ToList();
            var senders = await _users.Find(u => senderIds.Contains(u.Id)).ToListAsync();

            // Fetch friends list
            var currentUser = await _users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
            var friends = new List<UserModel>();
            if (currentUser?.Friends != null && currentUser.Friends.Any())
                friends = await _users.Find(u => currentUser.Friends.Contains(u.Id)).ToListAsync();

            return View(new Tuple<IEnumerable<UserModel>, IEnumerable<UserModel>>(senders, friends));
        }
        [HttpGet]
        public async Task<IActionResult> Profile(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "User not found" });

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            return Json(new
            {
                success = true,
                username = user.Username,
                email = user.Email,
                photoUrl = string.IsNullOrEmpty(user.PhotoUrl) ? "/images/default.png" : user.PhotoUrl
            });
        }

        [HttpGet]
        public async Task<IActionResult> CurrentUserProfile()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Not logged in" });

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            return Json(new
            {
                success = true,
                username = user.Username,
                email = user.Email,
                photoUrl = user.PhotoUrl // if exists
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCurrentUserPhoto()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Not logged in" });

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            var form = HttpContext.Request.Form;
            user.Username = form["Username"];
            user.Email = form["Email"];

            var file = HttpContext.Request.Form.Files["PhotoFile"];
            if (file != null && file.Length > 0)
            {
                var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(imagesDir))
                    Directory.CreateDirectory(imagesDir);

                // ✅ Delete old photo if it exists
                if (!string.IsNullOrEmpty(user.PhotoUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.PhotoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete old photo: {ex.Message}");
                        }
                    }
                }

                // ✅ Save new photo
                var fileName = $"{userId}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(imagesDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                user.PhotoUrl = "/images/" + fileName;
            }

            await _users.ReplaceOneAsync(u => u.Id == userId, user);

            return Json(new
            {
                success = true,
                message = "Profile updated successfully",
                photoUrl = user.PhotoUrl
            });
        }


        public class UpdateUserModel
        {
            public string Username { get; set; }
            public string Email { get; set; }
            public string PhotoUrl { get; set; }
        }

    }
}
