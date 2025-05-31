using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;               // for SignOutAsync(...)
using Microsoft.AspNetCore.Authentication.Cookies;
using DotNetCoreSqlDb.Helpers;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, student, teacher")]
    public class AccountManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IEmailSender _mailer;
        private readonly ILogger<AccountManagementController> _logger;
        private readonly LogHelper _logHelper;

        public AccountManagementController(MyDatabaseContext context, IEmailSender mailer, ILogger<AccountManagementController> logger, LogHelper logHelper)
        {
            _context = context;
            _mailer = mailer;
            _logger = logger;
            _logHelper = logHelper;
        }

        // GET: /AccountManagement/ChangePassword?userId=…
        [HttpGet]
        public async Task<IActionResult> ChangePassword(Guid userId)
        {
            ViewBag.UserId = userId;

            var user = await _context.User.FirstOrDefaultAsync(u => u.ID == userId);

            if (user == null)
            {
                return NotFound();
            }

            //If this is a required change, they don't have the option to go "back" to index
            if (user.MustChangePassword == true)
            {
                ViewBag.RequireChange = true;
            }
            else
            {
                ViewBag.RequireChange = false;
            }

            var returnController = "";
            string role = user.Role;

            switch (role)
            {
                case "student":
                    returnController = "Students";
                    break;
                case "teacher":
                    returnController = "Teachers";
                    break;
                case "support":
                    returnController = "Support";
                    break;
                default:
                    returnController = null;
                    break;
            }
            ViewBag.ReturnController = returnController;
            return View();
        }


        // POST: /AccountManagement/PasswordReset
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            Guid userId,
            string newPassword,
            string confirmPassword)
        {
            // simple server-side validation
            if (string.IsNullOrEmpty(newPassword)
                || newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords must be non-empty and match.";
                ViewBag.UserId = userId;
                return View();
            }

            //if (newPassword == )

            var user = await _context.User.FindAsync(userId);
            if (user == null)
                return NotFound();

            if (PasswordHelper.VerifyPassword(newPassword, user.PasswordHash, user.PasswordSalt))
            {
                ViewBag.Error = "New password must differ from your previous password.";
                ViewBag.UserId = userId;
                return View();
            }

            // update hash & salt
            PasswordHelper.CreatePasswordHash(
                newPassword,
                out var hash,
                out var salt);


            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.MustChangePassword = false;

            await _context.SaveChangesAsync();


            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Login");
        }

        // GET: /AccountManagement/PasswordReset?userId=…
        [HttpGet]
        public async Task<IActionResult> ChangeUsername(Guid userId)
        {
            ViewBag.UserId = userId;
            var user = await _context.User.FindAsync(userId);

            var returnController = "";
            string role = user.Role;

            switch (role)
            {
                case "student":
                    returnController = "Students";
                    break;
                case "teacher":
                    returnController = "Teachers";
                    break;
                case "support":
                    returnController = "Support";
                    break;
                default:
                    returnController = null;
                    break;
            }

            //return RedirectToAction("Index", returnController);
            ViewBag.ReturnController = returnController;

            return View();
        }

        // POST: /AccountManagement/PasswordReset
        //[AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUsername(
            Guid userId,
            string newUsername,
            string confirmUsername)
        {
            // simple server-side validation
            if (string.IsNullOrEmpty(newUsername)
                || newUsername != confirmUsername)
            {
                ViewBag.Error = "The usernames must be non-empty and match.";
                ViewBag.UserId = userId;
                return View();
            }

            if (newUsername != newUsername.Trim().Replace(" ", ""))
            {
                ViewBag.Error = "The username cannot have spaces in it.";
                ViewBag.UserId = userId;
                return View();
            }

            var user = await _context.User.FindAsync(userId);
            if (user == null)
                return NotFound();

            /*if (PasswordHelper.VerifyPassword(newPassword, user.PasswordHash, user.PasswordSalt))
            {
                ViewBag.Error = "New password must differ from your previous password.";
                ViewBag.UserId = userId;
                return View();
            }*/

            // update hash & salt
            /*PasswordHelper.CreatePasswordHash(
                newPassword,
                out var hash,
                out var salt);*/

            /*user.PasswordHash       = hash;
            user.PasswordSalt       = salt;
            user.MustChangePassword = false;*/

            bool exists = await _context.User.AnyAsync(u => u.Username == newUsername);

            if (exists)
            {
                ViewBag.Error = "This username is already taken. Please try another one.";
                ViewBag.UserId = userId;
                return View();
            }
            else
            {
                user.Username = newUsername;
            }

            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            // this will swap the URL in history rather than stacking a new one
            var loginUrl = Url.Action("Index", "Login");
            var script = $@"
                <script>
                    window.history.replaceState(null, null, '{loginUrl}');
                    window.location.replace('{loginUrl}');
                </script>";

            return Content(script, "text/html");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ForgotPassword(Guid userId)
        {
            // we’ll need this in the form’s hidden field
            ViewBag.UserId = userId;

            var user = await _context.User
                .FirstOrDefaultAsync(u => u.ID == userId);

            if (user?.Role.ToLower() == "student")
            {
                var student = await _context.Student
                    .Include(s => s.Contacts)
                    .FirstOrDefaultAsync(s => s.ID == userId);

                if (student == null)
                    return NotFound();

                var primaryEmail = student.Contacts?
                        .FirstOrDefault(c =>
                        string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase) &&
                        c.Invitation)
                        ?.Value;

                var obfuscatedEmail = "";

                if (primaryEmail != null)
                {
                    obfuscatedEmail = ObfuscateEmailHelper.ObfuscateEmail(primaryEmail);
                }
                
                if (!string.IsNullOrWhiteSpace(primaryEmail))
                    ViewBag.Email = obfuscatedEmail;
                else
                    ViewBag.Error = "We could not find an email for your account. Please contact support.";

                return View();
            }

            return Forbid();
        }

        // POST: /AccountManagement/ForgotPassword
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("ForgotPassword")]
        public async Task<IActionResult> ForgotPasswordPost(Guid userId)
        {
            ViewBag.UserId = userId;

            var user = await _context.User
                .FirstOrDefaultAsync(u => u.ID == userId);

            var role = "";

            if (user != null)
            {
                role = user.Role;
            }
            else
            {
                return NotFound();
            }

            if (role == "student")
            {
                var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == userId);
                if (student == null)
                    return NotFound();

                // Check for primary email before resetting password
                var primaryEmail = student.Contacts?
                    .FirstOrDefault(c =>
                        string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase) &&
                        c.Invitation == true)
                    ?.Value;

                if (string.IsNullOrWhiteSpace(primaryEmail))
                {
                    // Do NOT reset IncorrectAttempts or password if no email is on record
                    ViewBag.Error = "We could not find an email for your account. Please contact support.";
                    return View();
                }

                // Generate temporary password
                var rnd = new Random();
                string tempPassword = rnd.Next(100000, 999999).ToString();

                // Create hash & salt
                PasswordHelper.CreatePasswordHash(tempPassword, out byte[] hash, out byte[] salt);

                // Update user credentials
                //var user = await _context.User.FindAsync(userId);
                if (user == null)
                    return NotFound();

                user.PasswordHash = hash;
                user.PasswordSalt = salt;
                user.MustChangePassword = true;
                user.IncorrectAttempts = 0;

                _context.User.Update(user);
                await _context.SaveChangesAsync();

                if (primaryEmail == null)
                {
                    try
                    {
                        var html = $"""
                        <h2>Hello {student.Name}!</h2>
                        <p>You have requested to reset your password at <strong>torontofrench.com</strong>.</p>
                        <p><strong>Temporary Password:</strong> {tempPassword}</p>
                        <p>You will need to change this temporary password upon your next login.</p>
                        """;

                        string to = "michael.kamochkin@gmail.com";
                        string subject = "Password Reset Request";
                        await _mailer.SendAsync(
                            to: to,
                            subject: subject,
                            htmlBody: html);

                        await _logHelper.LogMailAsync(to, subject, html);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send password reset e-mail to student {StudentId}", student.ID);
                    }
                }
                else
                {
                    try
                    {
                        var html = $"""
                        <h2>Hello {student.Name}!</h2>
                        <p>You have requested to reset your password at <strong>torontofrench.com</strong>.</p>
                        <p><strong>Temporary Password:</strong> {tempPassword}</p>
                        <p>You will need to change this temporary password upon your next login.</p>
                        """;

                        string to = primaryEmail;
                        string subject = "Password Reset Request";
                        await _mailer.SendAsync(
                            to: primaryEmail,
                            subject: subject,
                            htmlBody: html);

                        await _logHelper.LogMailAsync(to, subject, html);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send password reset e-mail to student {StudentId}", student.ID);
                    }
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();

                // this will swap the URL in history rather than stacking a new one
                var loginUrl = Url.Action("Index", "Login");
                var script = $@"
                    <script>
                        window.history.replaceState(null, null, '{loginUrl}');
                        window.location.replace('{loginUrl}');
                    </script>";

                return Content(script, "text/html");
            }
            else
            {
                return Forbid();
            }


        }
    }
}
