using ChatAppMongo.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatAppMongo.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        private readonly IMongoCollection<UserModel> _users;

        public AccountController(IMongoDatabase db)
        {
            _users = db.GetCollection<UserModel>("Users");
        }

        public IActionResult Register() => View();
        [HttpPost]
        public async Task<IActionResult> Register(UserModel UserModel)
        {
            await _users.InsertOneAsync(UserModel);
            return RedirectToAction("Login");
        }


        public IActionResult Login() => View();
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Trim input
            username = username?.Trim();
            password = password?.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Username and password are required";
                return View();
            }

            // Find user
            var user = await _users
                .Find(x => x.Username == username && x.Password == password)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                ViewBag.Error = "Invalid username or password";
                return View();
            }

            // Login success
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            return RedirectToAction("Friends", "Chat");
        }
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }
    }
}
