using ChatAppMongo.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatAppMongo.Controllers
{
    public class GroupController : Controller
    {
        private readonly IMongoCollection<ChatGroup> _groupCollection; 
        private readonly IMongoCollection<ChatMessage> _chatCollection;

        public GroupController(IMongoDatabase db)
        {
            _groupCollection = db.GetCollection<ChatGroup>("ChatGroups");
            _chatCollection = db.GetCollection<ChatMessage>("ChatMessages");
        }
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string groupId, string query)

        {
            if (string.IsNullOrEmpty(groupId) || string.IsNullOrWhiteSpace(query))
                return Json(new { success = false, users = new List<object>() });

            var group = await _groupCollection.Find(g => g.Id == groupId).FirstOrDefaultAsync();
            if (group == null)
                return NotFound(new { success = false, message = "Group not found" });

            var usersCollection = HttpContext.RequestServices.GetService<IMongoDatabase>()
                .GetCollection<UserModel>("Users");

            var filter = Builders<UserModel>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i")) &
                         Builders<UserModel>.Filter.Nin(u => u.Id, group.Members);

            var users = await usersCollection.Find(filter).ToListAsync();

            var result = users.Select(u => new
            {
                id = u.Id,
                username = u.Username,
                photoUrl = u.PhotoUrl
            });

            return Json(new { success = true, users = result });
        }
        [HttpPost]
        public async Task<IActionResult> AddMember(string groupId, string userId)
        {
            if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Invalid parameters" });

            var filter = Builders<ChatGroup>.Filter.Eq(g => g.Id, groupId);
            var update = Builders<ChatGroup>.Update.AddToSet(g => g.Members, userId);

            var result = await _groupCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Failed to add member" });
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromForm] string groupName)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "User not authenticated" });

            if (string.IsNullOrWhiteSpace(groupName))
                return Json(new { success = false, message = "Group name required" });

            var group = new ChatGroup
            {
                Name = groupName,
                CreatedBy = userId,
                Members = new List<string> { userId },
                IconUrl = null
            };

            await _groupCollection.InsertOneAsync(group);


            return Json(new
            {
                success = true,
                group = new { id = group.Id, name = group.Name, iconUrl = group.IconUrl ?? "" }
            });
        }


        [HttpPost("RequestJoin")]
        public async Task<IActionResult> RequestJoin(string groupId, string userId)
        {
            var filter = Builders<ChatGroup>.Filter.Eq(g => g.Id, groupId);
            var update = Builders<ChatGroup>.Update.AddToSet(g => g.PendingRequests, userId);
            await _groupCollection.UpdateOneAsync(filter, update);
            return Json(new { success = true });
        }

        [HttpPost("AcceptMember")]
        public async Task<IActionResult> AcceptMember(string groupId, string userId)
        {
            var filter = Builders<ChatGroup>.Filter.Eq(g => g.Id, groupId);
            var update = Builders<ChatGroup>.Update
                .Pull(g => g.PendingRequests, userId)
                .AddToSet(g => g.Members, userId);
            await _groupCollection.UpdateOneAsync(filter, update);
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> MyGroups()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
                return Json(new List<ChatGroup>());

            var groups = await _groupCollection.Find(g => g.Members.Contains(userId)).ToListAsync();
            return Json(groups);
        }
        [HttpGet]
        public async Task<IActionResult> GroupChat(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return NotFound("GroupId is required");

            var group = await _groupCollection.Find(g => g.Id == groupId).FirstOrDefaultAsync();
            if (group == null)
                return NotFound("Group not found");

            var usersCollection = HttpContext.RequestServices.GetService<IMongoDatabase>()
                .GetCollection<UserModel>("Users");

            var members = await usersCollection
                .Find(u => group.Members.Contains(u.Id))
                .Project(u => new { u.Id, u.Username, u.PhotoUrl, u.Email })
                .ToListAsync();

            ViewBag.Members = members;

            ViewBag.CurrentUserId = HttpContext.Session.GetString("UserId");

            return View(group);
        }


        [HttpGet]
        public async Task<IActionResult> GetGroupMessages(string groupId)
        {
            var messages = await _chatCollection
                .Find(m => m.GroupId == groupId)
                .Sort(Builders<ChatMessage>.Sort.Ascending(m => m.Timestamp))
                .ToListAsync();

            return Json(messages.Select(m => new
            {
                m.Id,
                m.SenderId,
                m.Message,
                m.FileUrl,
                m.FileName,
                Timestamp = m.Timestamp.ToString("o")   
            }));
        }

    }
}
