using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DotNetCoreSqlDb.Controllers
{
    public class AccountManagementController : Controller
    {
        private readonly MyDatabaseContext _context;

        public AccountManagementController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: /AccountManagement/PasswordReset?userId=…
        //[AllowAnonymous]
        [HttpGet]
        public IActionResult PasswordReset(Guid userId)
        {
            // we’ll need this in the form’s hidden field
            ViewBag.UserId = userId;
            return View();
        }

        // POST: /AccountManagement/PasswordReset
        //[AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PasswordReset(
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
    }
}
