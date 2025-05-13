using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotNetCoreSqlDb.Services;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize]
    public class StudentManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<StudentManagementController> _logger;
        private readonly IEmailSender _mailer;

        public StudentManagementController(
            MyDatabaseContext context,
            ILogger<StudentManagementController> logger,
            IEmailSender mailer)
        {
            _context = context;
            _logger  = logger;
            _mailer  = mailer;
        }

        

        // GET: Students
        public async Task<IActionResult> Index(string sortOrder)
        {
            ViewBag.CurrentSort = sortOrder;
            // Include Contacts so that we can display email addresses.
            IQueryable<Student> query = _context.Student.Include(s => s.Contacts);

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (!isPrivileged)
            {
                query = query.Where(s => s.AccountingGroup == "S");
            }

            switch (sortOrder)
            {
                case "Name":
                    query = query.OrderBy(s => s.Name);
                    break;
                case "name_desc":
                    query = query.OrderByDescending(s => s.Name);
                    break;
                case "CreatedDate":
                    query = query.OrderBy(s => s.CreatedDate);
                    break;
                case "createdDate_desc":
                    query = query.OrderByDescending(s => s.CreatedDate);
                    break;
                default:
                    query = query.OrderBy(s => s.Name);
                    break;
            }
            return View(await query.ToListAsync());
        }

        // GET: Students/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (!isPrivileged && student.AccountingGroup != "S")
                return Forbid();

            return View(student);
        }

        // GET: Students/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == id);
            if (student == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (!isPrivileged && student.AccountingGroup != "S")
                return Forbid();

            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
            return View(student);
        }

        // POST: Students/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Student updatedStudent)
        {
            if (id != updatedStudent.ID)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ContactTypes         = DropdownOptions.ContactTypes;
                ViewBag.SourceTypes          = DropdownOptions.SourceTypes;
                ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                ViewBag.Timezones            = TimeZoneMapping.GetTimeZones();
                return View(updatedStudent);
            }

            var existingStudent = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == id);

            if (existingStudent == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (!isPrivileged && existingStudent.AccountingGroup != "S")
                return Forbid();

            // --- scalar updates ---
            existingStudent.Name            = updatedStudent.Name;
            existingStudent.ParentOrEmployer= updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes       = updatedStudent.MainNotes;
            existingStudent.Source          = updatedStudent.Source;
            existingStudent.TimeZoneId      = updatedStudent.TimeZoneId;
            existingStudent.AccountingGroup = isPrivileged ? updatedStudent.AccountingGroup : "S";

            // --- rename solo groups ---
            var suffix = string.IsNullOrWhiteSpace(existingStudent.ParentOrEmployer) ? "" : " – " + existingStudent.ParentOrEmployer;
            var studentNamePlusParent = existingStudent.Name + suffix;

            var soloGroups = await _context.Group
                .Where(g => g.StudentGroupCompositions.Count() == 1 &&
                            g.StudentGroupCompositions.Any(c => c.StudentId == id))
                .ToListAsync();

            foreach (var g in soloGroups)
                g.Name = studentNamePlusParent;

            // --- sync contacts ---
            if (updatedStudent.Contacts != null && updatedStudent.Contacts.Any())
            {
                foreach (var contact in updatedStudent.Contacts)
                {
                    if (contact.ID == Guid.Empty || contact.ID.ToString() == "00000000-0000-0000-0000-000000000000")
                    {
                        contact.ID        = Guid.NewGuid();
                        contact.StudentID = existingStudent.ID;
                        _context.Entry(contact).State = EntityState.Added;
                        existingStudent.Contacts.Add(contact);
                    }
                    else
                    {
                        var existingContact = existingStudent.Contacts.FirstOrDefault(c => c.ID == contact.ID);
                        if (existingContact != null)
                        {
                            existingContact.Type       = contact.Type;
                            existingContact.Value      = contact.Value;
                            existingContact.Invitation = contact.Invitation;
                            existingContact.Emergency  = contact.Emergency;
                            existingContact.Money      = contact.Money;
                        }
                    }
                }

                var postedIds = updatedStudent.Contacts
                    .Where(c => c.ID != Guid.Empty && c.ID.ToString() != "00000000-0000-0000-0000-000000000000")
                    .Select(c => c.ID)
                    .ToList();

                var toRemove = existingStudent.Contacts
                    .Where(c => c.ID != Guid.Empty && !postedIds.Contains(c.ID))
                    .ToList();

                foreach (var c in toRemove)
                    _context.Entry(c).State = EntityState.Deleted;
            }

            // --- NEW: create user if missing ---
            bool userExists = await _context.User.AnyAsync(u => u.ID == existingStudent.ID);
            if (!userExists)
            {
                var rnd       = new Random();
                var tempPass  = rnd.Next(100000, 1000000).ToString();

                PasswordHelper.CreatePasswordHash(tempPass, out byte[] hash, out byte[] salt);

                const int maxTries = 899;
                int tries = 0;
                string clean = new string(existingStudent.Name.Where(c => !char.IsWhiteSpace(c)).ToArray());
                string candidate;
                bool candidateExists;
                do
                {
                    int suffixNum = rnd.Next(100, 1000);
                    candidate = $"{clean}{suffixNum}";
                    candidateExists = await _context.User.AnyAsync(u => u.Username == candidate);
                }
                while (candidateExists && ++tries < maxTries);

                if (tries >= maxTries)
                {
                    ViewBag.Error = "Could not generate a unique username for this user.";
                    ViewBag.ContactTypes         = DropdownOptions.ContactTypes;
                    ViewBag.SourceTypes          = DropdownOptions.SourceTypes;
                    ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                    ViewBag.Timezones            = TimeZoneMapping.GetTimeZones();
                    return View(updatedStudent);
                }

                var newUser = new User
                {
                    ID                = existingStudent.ID,
                    Username          = candidate,
                    Role              = "student",
                    MustChangePassword= true,
                    PasswordHash      = hash,
                    PasswordSalt      = salt,
                    IncorrectAttempts = 0
                };
                _context.User.Add(newUser);

                try
                {
                    var html = $"""
                        <h2>Bienvenue {existingStudent.Name} !</h2>
                        <p>Your temporary credentials at <strong>TorontoFrench.com</strong>.</p>
                        <p>
                            <strong>Username :</strong> {candidate}<br/>
                            <strong>Temporary Password :</strong> {tempPass}
                        </p>
                        <p>You will have to change your password (and optionally username) upon your first login.</p>
                        """;

                    await _mailer.SendAsync(
                        to:      "michael.kamochkin@gmail.com",
                        subject: "Your credentials at Toronto French",
                        htmlBody: html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send welcome e-mail to student {StudentId}", existingStudent.ID);
                }
            }

            // --- persist ---
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Student.Any(e => e.ID == updatedStudent.ID))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Students/Create
        public IActionResult Create()
        {
            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
            Student student = new Student { Name = string.Empty };
            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (isPrivileged)
            {
                student.AccountingGroup = "A";
            }
            var token = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("CreateStudentToken", token);
            ViewBag.FormToken = token;
            return View(student);
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")]
            Student student)
        {
            // 1) Validate your one-time token
            var formToken    = Request.Form["FormToken"].ToString();
            var sessionToken = HttpContext.Session.GetString("CreateStudentToken");
            if (formToken != sessionToken)
            {
                // either a repeat-submit or invalid token -> drop it
                return RedirectToAction(nameof(Index));
            }
            // consume it so it can’t be used again
            HttpContext.Session.Remove("CreateStudentToken");

            // set created date
            student.CreatedDate = DateTime.Now;

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            // non-admin/supports get forced into group "S"
            if (!isPrivileged)
            {
                student.AccountingGroup = "S";
            }

            // require at least one contact
            if (student.Contacts == null || !student.Contacts.Any())
            {
                ModelState.AddModelError("", "Please add at least one contact.");
            }

            if (ModelState.IsValid)
            {
                // 1) Save the new Student (and its Contacts)
                student.ID = Guid.NewGuid();
                _context.Add(student);
                //await _context.SaveChangesAsync();

                var suffix   = string.IsNullOrWhiteSpace(student.ParentOrEmployer)
                   ? ""
                   : " – " + student.ParentOrEmployer;
                var studentNamePlusParent = student.Name + suffix;

                // 2) Create a Group just for this student
                var group = new Group
                {
                    Id   = Guid.NewGuid(),
                    Name = studentNamePlusParent,
                    IsActive = true            
                    
                };
                _context.Group.Add(group);
                //await _context.SaveChangesAsync();

                // 3) Link the student into that group
                var composition = new StudentGroupComposition
                {
                    Id           = Guid.NewGuid(),
                    GroupId      = group.Id,
                    StudentId    = student.ID,
                    UseMyBalance = true      // or whatever default you prefer
                };
                _context.StudentGroupComposition.Add(composition);

                Random rnd = new Random();
                string defaultPassword = rnd.Next(100000,1000000) + "";
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
                        student.Name
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

                    if(++tries >= maxTries)
                    {
                        ViewBag.Error = "Could not generate a unique username for this user.";
                        break;
                        //return View();
                        //throw new InvalidOperationException("Could not generate a unique username.");
                    }
                    // (Optional) you could also keep a counter and bail out after N attempts
                } 
                // keep trying *while* it already exists
                while (exists);

                var user = new User {
                    ID                 = student.ID,
                    Username           = candidate,
                    Role               = "student",    // or whatever default
                    MustChangePassword = true,
                    PasswordHash       = passwordHash,
                    PasswordSalt       = passwordSalt,
                    IncorrectAttempts = 0
                    
                };
                _context.User.Add(user);

                var primaryEmail = student.Contacts?
                    .FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                
                if (!string.IsNullOrWhiteSpace(primaryEmail))
                 {
                     try
                     {
                         var html = $"""
                             <h2>Bienvenue {student.Name} !</h2>
                             <p>Your temporary credentials at <strong>TorontoFrench.com</strong>.</p>
                             <p>
                                 <strong>Username :</strong> {candidate}<br/>
                                 <strong>Temporary Password :</strong> {defaultPassword}
                             </p>
                             <p>You will have to change your password (and optionally username) upon your first login.</p>
                             """;

                            /*await _mailer.SendAsync(
                             to:       primaryEmail,
                             //from: "Toronto French",
                             subject:  "Your credentials at Toronto French",
                             htmlBody: html);*/

                             await _mailer.SendAsync(
                             to:       "michael.kamochkin@gmail.com",
                             //from: "Toronto French",
                             subject:  "Your credentials at Toronto French",
                             htmlBody: html);
                             
                     }
                     catch (Exception ex)
                     {
                         _logger.LogError(ex,
                             "Failed to send welcome e-mail to student {StudentId}", student.ID);
                         // swallow → the user is created even if mail fails
                     }
                 }


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
                        ViewBag.ContactTypes = DropdownOptions.ContactTypes;
                        ViewBag.SourceTypes = DropdownOptions.SourceTypes;
                        ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                        ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
                        var newToken = Guid.NewGuid().ToString();
                        HttpContext.Session.SetString("CreateStudentToken", newToken);
                        ViewBag.FormToken = newToken;
                        return View(student);
                    }
                    else
                    {
                        // Some other database error → rethrow it
                        throw;
                    }
                }

            }

            // if we hit errors, repopulate the dropdowns and show the form again
            ViewBag.ContactTypes         = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes          = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones            = TimeZoneMapping.GetTimeZones();
            return View(student);
        }

        // DeleteContact action for deleting a single contact.
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteContact(Guid contactId)
        {
            var contact = await _context.Contact.FindAsync(contactId);
            if (contact == null)
            {
                return Json(new { success = false, message = "Contact not found." });
            }
            _context.Contact.Remove(contact);
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
