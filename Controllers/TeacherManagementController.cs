using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotNetCoreSqlDb.Services;
using DotNetCoreSqlDb.Helpers;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin")]
    public class TeacherManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<TeacherManagementController> _logger;
        private readonly IEmailSender _mailer;
        private readonly LogHelper _logHelper;

        public TeacherManagementController(
            MyDatabaseContext context,
            ILogger<TeacherManagementController> logger,
            IEmailSender mailer,
            LogHelper logHelper)
        {
            _context = context;
            _logger = logger;
            _mailer = mailer;
            _logHelper = logHelper;
        }

        

        // GET: Students
        public async Task<IActionResult> Index(string sortOrder)
        {
            var teachers = await _context.Teacher
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(teachers);
        }

        // GET: Teacher/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teacher
                .FirstOrDefaultAsync(t => t.Id == id);
            if (teacher == null)
                return NotFound();

            return View(teacher);
        }

        // GET: Students/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teacher
                .FirstOrDefaultAsync(s => s.Id == id);
            if (teacher == null)
                return NotFound();

            return View(teacher);
        }

        // POST: Teacher/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Teacher updatedTeacher)
        {
            var existingTeacher = await _context.Teacher
                .FirstOrDefaultAsync(s => s.Id == id);

            if (existingTeacher == null)
                return NotFound();

            existingTeacher.Name = updatedTeacher.Name;
            existingTeacher.Email = updatedTeacher.Email;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Teacher.Any(e => e.Id == updatedTeacher.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Teacher/Create
        public IActionResult Create()
        {
            
            Teacher teacher = new Teacher { Name = string.Empty };
    
            var token = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("CreateTeacherToken", token);
            ViewBag.FormToken = token;

            return View(teacher);
        }

        // POST: Teacher/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            //[Bind("Id,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")]
            Teacher teacher)
        {
            // 1) Validate your one-time token
            var formToken    = Request.Form["FormToken"].ToString();
            var sessionToken = HttpContext.Session.GetString("CreateTeacherToken");
            if (formToken != sessionToken)
            {
                // either a repeat-submit or invalid token -> drop it
                return RedirectToAction(nameof(Index));
            }
            // consume it so it can’t be used again
            HttpContext.Session.Remove("CreateTeacherToken");

            teacher.TimeZoneId = "America/New_York";
            
            if (ModelState.IsValid)
            {
                // 1) Save the new Student (and its Contacts)
                teacher.Id = Guid.NewGuid();
                _context.Add(teacher);

                Random rnd = new Random();
                string defaultPassword = rnd.Next(100000, 1000000) + "";
                //Random rnd = new Random();
                //int rndAppend = rnd.Next(0, 1000);

                PasswordHelper.CreatePasswordHash(
                    defaultPassword,
                    out byte[] passwordHash,
                    out byte[] passwordSalt
                );

                //bool exists = await _context.User.AnyAsync(u => u.Username == student.Name + rndAppend);

                // 1) Seed Random once

                string candidate;
                bool exists;
                int tries = 0;
                const int maxTries = 899;

                string clean = new string(
                        teacher.Name
                            .Where(c => !char.IsWhiteSpace(c))
                            .ToArray()
                    );
                // 2) Loop *after* generating the candidate
                do
                {
                    int usernameSuffix = rnd.Next(100, 1000);

                    candidate = $"{clean}{usernameSuffix}";

                    exists = await _context.User
                        .AnyAsync(u => u.Username == candidate);

                    if (++tries >= maxTries)
                    {
                        ViewBag.Error = "Could not generate a unique username for this user.";
                        break;
                    }
                }
                // keep trying *while* it already exists
                while (exists);

                var user = new User
                {
                    ID = teacher.Id,
                    Username = candidate,
                    Role = "teacher",
                    MustChangePassword = false,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    IncorrectAttempts = 0
                };
                _context.User.Add(user);

                /*var primaryEmail = student.Contacts?
                    .FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
                */

                //if (!string.IsNullOrWhiteSpace(primaryEmail))
                //{
                try
                {
                    var html = $"""
                             <h2>Bienvenue {teacher.Name} !</h2>
                             <p>Your temporary credentials at <strong>TorontoFrench.com</strong>.</p>
                             <p>
                                 <strong>Username :</strong> {candidate}<br/>
                                 <strong>Temporary Password :</strong> {defaultPassword}
                             </p>
                             <p>You will have to change your password (and optionally username) upon your first login.</p>
                             <p>
                                 Click here to log in: 
                                 <a href="https://msdocs-core-sql-tsl.azurewebsites.net/" target="_blank" style="color: #1a73e8;">Log in to your account</a>
                             </p>
                             """;

                    /*await _mailer.SendAsync(
                     to:       primaryEmail,
                     //from: "Toronto French",
                     subject:  "Your credentials at Toronto French",
                     htmlBody: html);*/
                    string to = "kamochkin@gmail.com";
                    string subject = "Your credentials at Toronto French";
                    await _mailer.SendAsync(
                    to: to,
                    //from: "Toronto French",
                    subject: subject,
                    htmlBody: html);

                    await _logHelper.LogMailAsync(to, subject, html);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send welcome e-mail to student {StudentId}", teacher.Id);
                    // swallow → the user is created even if mail fails
                }
                //}


                // 4) Create a blank Assignments entry for the new group
                /*var assignment = new Assignments
                {
                    Id                  = Guid.NewGuid(),
                    GroupId             = group.Id,
                    TeacherId           = null,            // now allowed as nullable
                    StudentUnitCost     = 0f,
                    StudentUnitType     = string.Empty,
                    StudentUnitBalance  = 0f,
                    StudentUnitDuration = 0f,
                    TeacherPayForUnit   = 0f,
                    TeacherPayUnitType  = string.Empty,
                    IsActive            = false
                };
                _context.Assignments.Add(assignment);
                */
                // 5) Persist composition + assignment
                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("IX_User_Username"))
                    {
                        // SQL Server threw a duplicate username constraint error
                        ViewBag.Error = "Could not generate a unique username for this user.";

                        // Repopulate dropdowns and return the Create view again

                        var newToken = Guid.NewGuid().ToString();
                        HttpContext.Session.SetString("CreateTeacherToken", newToken);
                        ViewBag.FormToken = newToken;
                        return View(teacher);
                    }
                    else
                    {
                        throw;
                    }
                }

            }

            return View(teacher);
        }
    }
}
