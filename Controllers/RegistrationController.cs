using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotNetCoreSqlDb.Services;
using FuzzySharp;
using DotNetCoreSqlDb.Helpers;
using System.Text.RegularExpressions;

namespace DotNetCoreSqlDb.Controllers
{
    //[Authorize(Roles = "support, admin, assistant")]
    public class RegistrationController : Controller
    {
        private readonly MyDatabaseContext _context;
        //private readonly ILogger<RegistrationController> _logger;
        private readonly IEmailSender _mailer;
        private readonly LogHelper _logHelper;

        public RegistrationController(
            MyDatabaseContext context,
            //ILogger<StudentManagementController> logger,
            IEmailSender mailer,
            LogHelper logHelper)
        {
            _context = context;
            //_logger = logger;
            _mailer = mailer;
            _logHelper = logHelper;
        }

        // GET: Students
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // POST: /Registration/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            string studentName,
            string studentEmail)
        {

            if (string.IsNullOrEmpty(studentName) || string.IsNullOrEmpty(studentEmail))
            {
                ViewBag.Error = "Name and Email fields must be non-empty.";
                return View();
            }

            bool emailAlreadyExists = await _context.Contact
                                        .AnyAsync(e => e.Value.Contains(studentEmail));

            if (emailAlreadyExists)
            {
                ViewBag.Error = "A student with this email already exists. Please check your email inbox for your credentials.";
                return View();
            }

            Guid studentID = Guid.NewGuid();

            var student = new Student
            {
                Name = studentName,
                ParentOrEmployer = null,
                MainNotes = null,
                CreatedDate = DateTime.Now,
                Source = "Registered by themselves",
                TimeZoneId = "America/New_York",
                AccountingGroup = "S",
                ID = studentID,
                Status = null
            };
            _context.Student.Add(student);

            var contact = new Contact
            {
                Value = studentEmail,
                Type = "Email",
                StudentID = studentID,
                Invitation = true,
                ID = Guid.NewGuid()
            };
            _context.Contact.Add(contact);

            var group = new DotNetCoreSqlDb.Models.Group
            {
                Id = Guid.NewGuid(),
                Name = studentName,
                IsActive = true,
                IsManualGroup = false
            };
            _context.Group.Add(group);

            var composition = new StudentGroupComposition
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                StudentId = studentID,
                UseMyBalance = true
            };
            _context.StudentGroupComposition.Add(composition);


            Random rnd = new Random();
            string defaultPassword = rnd.Next(100000, 1000000) + "";

            PasswordHelper.CreatePasswordHash(
                defaultPassword,
                out byte[] passwordHash,
                out byte[] passwordSalt
            );

            string candidate;
            bool exists = true;
            int tries = 0;
            const int maxTries = 5000;
            HashSet<string> attempted = new HashSet<string>();

            do
            {
                // Generate 6-digit body
                int body = rnd.Next(100000, 1000000); // 100000..999999
                string bodyStr = body.ToString();

                // Add 1-digit Luhn check
                int checkDigit = LuhnHelper.ComputeLuhnCheckDigit(bodyStr);
                candidate = bodyStr + checkDigit; // total length = 7

                if (!attempted.Add(candidate))
                    continue;

                // Sanity check
                if (!LuhnHelper.IsLuhnValid(candidate))
                    continue;

                exists = await _context.User.AnyAsync(u => u.Username == candidate);

                if (++tries >= maxTries)
                {
                    ViewBag.Error = "Could not generate a unique username for this user.";
                    break;
                }

            } while (exists);

            if (!exists)
            {
                var user = new User
                {
                    ID = studentID,
                    Username = candidate,
                    Role = "student",
                    MustChangePassword = false,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    IncorrectAttempts = 0
                };

                _context.User.Add(user);
            }

            try
            {
                var html = $"""
                             <h2>Welcome to TorontoFrench.com!</h2>
                             <p>Your credentials:</p>
                             <p>
                                 <strong>Student Number :</strong> {candidate}<br><br>

                                 <strong>Password :</strong> {defaultPassword}
                             </p>
                             <p>
                                 <a href="https://torontofrench.ca/" target="_blank" style="color: #1a73e8;">Click here to log in</a>
                             </p>
                             """;


                string to = studentEmail;
                string subject = "TorontoFrench.com credentials";
                await _mailer.SendAsync(
                to: to,
                subject: subject,
                htmlBody: html);

                //await _logHelper.LogMailAsync(to, subject, html);

            }
            catch (Exception ex)
            {
                //_logger.LogError("Failed to send welcome e-mail to student {StudentId}", studentEmail);
                // swallow → the user is created even if mail fails
            }

            try
            {
                await _context.SaveChangesAsync();
                //return RedirectToAction();
                return RedirectToAction("Index", "Login");
            }
            catch (DbUpdateException ex)
            {
                //_logger.LogInformation("error when creating a student through registration: {}", ex);
            }

            return RedirectToAction("Index", "Login");
        }
    }
}