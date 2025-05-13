using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Identity;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize]
    public class AccountManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IEmailSender _mailer;
        private readonly ILogger<AccountManagementController> _logger;

        public AccountManagementController(MyDatabaseContext context, IEmailSender mailer, ILogger<AccountManagementController> logger)
        {
            _context = context;
            _mailer = mailer;
            _logger = logger;
        }

        // GET: /AccountManagement/ChangePassword?userId=…
        [HttpGet]
        public async Task<IActionResult> ChangePassword(Guid userId)
        {
            // we’ll need this in the form’s hidden field
            ViewBag.UserId = userId;

            // NEW ─ determine if this is a forced-change scenario
            var user = await _context.User.FirstOrDefaultAsync(u => u.ID == userId);
            ViewBag.RequireChange = user?.MustChangePassword ?? false;

            return View();
        }


        // POST: /AccountManagement/PasswordReset
        //[AllowAnonymous]
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


            user.PasswordHash       = hash;
            user.PasswordSalt       = salt;
            user.MustChangePassword = false;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Login");
        }

        // GET: /AccountManagement/PasswordReset?userId=…
        //[AllowAnonymous]
        [HttpGet]
        public IActionResult ChangeUsername(Guid userId)
        {
            // we’ll need this in the form’s hidden field
            ViewBag.UserId = userId;
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
                ||newUsername != confirmUsername)
            {
                ViewBag.Error = "The usernames must be non-empty and match.";
                ViewBag.UserId = userId;
                return View();
            }

            if (newUsername != newUsername.Trim())
            {
                ViewBag.Error = "The username cannot have spaces in it.";
                ViewBag.UserId = userId;
                return View();
            }

            //if (newPassword == )

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

            return RedirectToAction("Index", "Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ForgotPassword(Guid userId)
        {
            // we’ll need this in the form’s hidden field
            ViewBag.UserId = userId;

            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == userId);
            
            if (student == null)
                return NotFound();

            var primaryEmail = student.Contacts?
                    .FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

            var obfuscatedEmail = ObfuscateEmailHelper.ObfuscateEmail(primaryEmail);

            if (!string.IsNullOrWhiteSpace(primaryEmail))
                ViewBag.Email = obfuscatedEmail;
            else
                ViewBag.Error = "We could not find an email for your account. Please contact support.";
            
            return View();
        }

        // POST: /AccountManagement/ForgotPassword
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("ForgotPassword")]
        public async Task<IActionResult> ForgotPasswordPost(Guid userId)
        {
            ViewBag.UserId = userId;

            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == userId);
            if (student == null)
                return NotFound();

            // Check for primary email before resetting password
            var primaryEmail = student.Contacts?
                .FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
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
            var user = await _context.User.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.PasswordHash       = hash;
            user.PasswordSalt       = salt;
            user.MustChangePassword = true;
            user.IncorrectAttempts  = 0;

            _context.User.Update(user);
            await _context.SaveChangesAsync();

            // Send reset email
            try
            {
                var html = $"""
                    <h2>Hello {student.Name}!</h2>
                    <p>You have requested to reset your password at <strong>TorontoFrench.com</strong>.</p>
                    <p><strong>Temporary Password:</strong> {tempPassword}</p>
                    <p>You will have to change your password upon your next login.</p>
                    """;

                await _mailer.SendAsync(
                    to:       "michael.kamochkin@gmail.com",
                    subject:  "Password Reset Request",
                    htmlBody: html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send password reset e-mail to student {StudentId}", student.ID);
            }

            return RedirectToAction("Index", "Login");
        }
    }
}
