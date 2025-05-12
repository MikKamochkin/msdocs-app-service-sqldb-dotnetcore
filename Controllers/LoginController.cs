using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DotNetCoreSqlDb.Controllers
{
    //[RequireHttps]
    public class LoginController : Controller
    {
        private readonly MyDatabaseContext _context;
        private const int maxAttempts = 6;

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
        public async Task<IActionResult> Index(string username, string password)
        {
            ViewBag.ShowForgotPassword = false;
            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both username and password.";
                return View();
            }

            var user = _context.User.FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials. Please try again.";
                return View();
            }

            //const int maxAttempts = 6;

            if (user.IncorrectAttempts >= maxAttempts)
            {
                ViewBag.Error = "You have reached the limit of incorrect sign-in attempts.";
                ViewBag.ShowForgotPassword = true;
                ViewBag.ForgotUserId = user.ID;
                return View();
            }

            bool valid = PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);

            if (!valid)
            {
                user.IncorrectAttempts++;
                await _context.SaveChangesAsync();

                ViewBag.Error = "Invalid credentials. Please try again.";
                ViewBag.ShowForgotPassword = user.IncorrectAttempts >= 1;
                ViewBag.ForgotUserId = user.ID;
                return View();
            }     
            
            user.IncorrectAttempts = 0;
            await _context.SaveChangesAsync();

            if (user.MustChangePassword == true)
            {
                return RedirectToAction("ChangePassword", "AccountManagement", new { userId = user.ID });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("UserID", user.ID.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // Create a claims identity specifying the authentication scheme
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Set up authentication properties if needed (e.g., IsPersistent for "Remember Me")
            var authProperties = new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1440),
                // IsPersistent = true
            };

            // Sign in the user (this creates an encrypted cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Redirect based on role
            if (user.Role.ToLower() == "student")
            {
                return RedirectToAction("Schedule", "Students");
            }
            else if (user.Role.ToLower() == "admin" || user.Role.ToLower() == "assistant" || user.Role.ToLower() == "support")  
            {
                return RedirectToAction("Index", "StudentManagement");
            }
            else
            {
                ViewBag.Error = "Invalid credentials. Please try again.";
                return View();
            }

            


            //Uncomment next 2 lines to create a password for a user through password change
            //bool emptyHashShortcut = user?.PasswordHash?.Length == 0 && user?.PasswordSalt?.Length == 0;
            //if (user != null && (emptyHashShortcut || PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt) ))
            /*if (user != null && PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt) && user.IncorrectAttempts < 6)
            {
                
                if (user.MustChangePassword == true){
                    return RedirectToAction(
                    actionName: "PasswordReset",
                    controllerName: "AccountManagement",
                    routeValues: new { userId = user.ID }
                );}
                
                // Create a list of claims. In a later step, you can add additional claims for permissions.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("UserID", user.ID.ToString()),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                // Create a claims identity specifying the authentication scheme
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // Set up authentication properties if needed (e.g., IsPersistent for "Remember Me")
                var authProperties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1440),
                    // IsPersistent = true
                };

                // Sign in the user (this creates an encrypted cookie)
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Redirect based on role
                if (user.Role.ToLower() == "student")
                {
                    return RedirectToAction("Schedule", "Students");
                }
                else if (user.Role.ToLower() == "admin" || user.Role.ToLower() == "assistant" || user.Role.ToLower() == "support")  
                {
                    return RedirectToAction("Index", "StudentManagement");
                }
                else
                {
                    ViewBag.Error = "Invalid credentials. Please try again.";
                    return View();
                }
            }
            else if (user != null && !PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt) && user.IncorrectAttempts < 6)
            {
                ViewBag.Error = "Invalid credentials. Please try again.";
                user.IncorrectAttempts++;
                return View();
            }
            else{
                ViewBag.Error = "You have reached the limit of incorrect sign in attempts. Please ";
                //user.IncorrectAttempts++;
                return View();
            }*/
        }

        // Simple logout action
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index");
        }
    }
}
