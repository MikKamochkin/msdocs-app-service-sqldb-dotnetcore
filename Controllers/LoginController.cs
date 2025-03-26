using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DotNetCoreSqlDb.Controllers
{
    [RequireHttps]
    public class LoginController : Controller
    {
        private readonly MyDatabaseContext _context;

        public LoginController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: /Login
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string name, string password)
        {
            // Validate input
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both name and password.";
                return View();
            }

            // Look up the user in the database (for testing, passwords are in plain text)
            var user = _context.User.FirstOrDefault(u => u.Name == name && u.Password == password);
            if (user != null)
            {
                // Create a list of claims. In a later step, you can add additional claims for permissions.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim("UserID", user.ID.ToString()),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                // Create a claims identity specifying the authentication scheme
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // Set up authentication properties if needed (e.g., IsPersistent for "Remember Me")
                var authProperties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                    // IsPersistent = true
                };

                // Sign in the user (this creates an encrypted cookie)
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Redirect to /Students/Index on successful login
                return RedirectToAction("Index", "Students");
            }
            else
            {
                ViewBag.Error = "Invalid credentials. Please try again.";
                return View();
            }
        }

        // Simple logout action
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index");
        }
    }
}
