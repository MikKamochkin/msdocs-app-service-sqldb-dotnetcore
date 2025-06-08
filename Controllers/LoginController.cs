using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using DotNetCoreSqlDb.Helpers;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DotNetCoreSqlDb.Controllers
{
    [RequireHttps]
    public class LoginController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly LogHelper _logHelper;
        private readonly IEmailSender _mailer;
        private const int maxAttempts = 6;

        public LoginController(MyDatabaseContext context, LogHelper logHelper, IEmailSender mailer)
        {
            _context = context;
            _logHelper = logHelper;
            _mailer = mailer;
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

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "Unknown";

            //ViewBag.ShowForgotPassword = true;
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
                await _logHelper.LogSignInAsync(username, ipAddress, false);
                return View();
            }

            //const int maxAttempts = 6;

            if (user.IncorrectAttempts >= maxAttempts)
            {
                ViewBag.Error = "You have reached the limit of incorrect sign-in attempts.";
                ViewBag.ShowForgotPassword = true;
                ViewBag.ForgotUserId = user.ID;
                await _logHelper.LogSignInAsync(user.ID, username, ipAddress, false);
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
                await _logHelper.LogSignInAsync(user.ID, username, ipAddress, false);
                return View();
            }

            user.IncorrectAttempts = 0;
            await _logHelper.LogSignInAsync(user.ID, username, ipAddress, true);
            await _context.SaveChangesAsync();

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

            if (user.MustChangePassword == true)
            {
                return RedirectToAction("ChangePassword", "AccountManagement", new { userId = user.ID });
            }

            // Redirect based on role
            if (user.Role.ToLower() == "student")
            {
                return RedirectToAction("Schedule", "Students");
            }
            else if (user.Role.ToLower() == "admin" || user.Role.ToLower() == "assistant" || user.Role.ToLower() == "support")
            {
                return RedirectToAction("Index", "StudentManagement");
            }
            else if (user.Role.ToLower() == "teacher")
            {
                return RedirectToAction("Schedule", "Teachers");
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

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }
        
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email.";
                return View();
            }

            bool moreThanOneEmail = _context.Contact
                                    .Count(r => r.Value == email) > 1;

            if (moreThanOneEmail)
            {
                ViewBag.Message = "Unable to send you a reset link, please contact us for support";
                return View();
            }
            // try to find a student with this email
            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.Contacts.Any(c => c.Type != null && c.Type.ToLower() == "email" && c.Value == email));

            var teacher = await _context.Teacher.FirstOrDefaultAsync(t => t.Email == email);

            User? user = null;
            if (student != null)
                user = await _context.User.FirstOrDefaultAsync(u => u.ID == student.ID);
            else if (teacher != null)
                user = await _context.User.FirstOrDefaultAsync(u => u.ID == teacher.Id);

            if (user != null)
            {
                var token = PasswordResetTokenStore.CreateToken(user.ID, TimeSpan.FromHours(1));
                var link = Url.Action("ResetPassword", "Login", new { token }, Request.Scheme);

                var html = $"If you requested a password reset, click <a href=\"{link}\">here</a>. If not, ignore this email.";
                var subject = "Password Reset";

                await _mailer.SendAsync(email, subject, html);
                await _logHelper.LogMailAsync(email, subject, html);
            }

            ViewBag.Message = "If the email exists, a reset link has been sent.";
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !PasswordResetTokenStore.TryRedeem(token, out var userId))
            {
                return RedirectToAction("Index");
            }

            var user = await _context.User.FindAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Index");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("UserID", user.ID.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15) });

            user.MustChangePassword = true;
            await _context.SaveChangesAsync();

            return RedirectToAction("ChangePassword", "AccountManagement", new { userId = user.ID });
        }

    }
}
