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
    [Authorize(Roles = "support, admin, assistant")]
    public class StudentManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<StudentManagementController> _logger;
        private readonly IEmailSender _mailer;
        private readonly LogHelper _logHelper;

        public StudentManagementController(
            MyDatabaseContext context,
            ILogger<StudentManagementController> logger,
            IEmailSender mailer,
            LogHelper logHelper)
        {
            _context = context;
            _logger = logger;
            _mailer = mailer;
            _logHelper = logHelper;
        }



        // GET: Students
        public async Task<IActionResult> Index(
            string? sortOrder,
            string? q,                 // search across name/email/phone/employer
            int page = 1,              // 1-based
            int pageSize = 50,         // tune as needed (25/50/100)
            CancellationToken ct = default)
        {
            sortOrder ??= "createdDate_desc";
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            // Base query (NO Include) — we will project only what we need
            var students = _context.Student.AsNoTracking();

            // Server-side search (LIKE). For phone search to be great, consider storing a normalized digits-only phone column.
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                students = students.Where(s =>
                    EF.Functions.Like(s.Name, pattern) ||
                    EF.Functions.Like(s.ParentOrEmployer, pattern) ||
                    s.Contacts.Any(c => EF.Functions.Like(c.Value, pattern)));
            }

            // Sorting
            students = sortOrder switch
            {
                "Name" => students.OrderBy(s => s.Name),
                "name_desc" => students.OrderByDescending(s => s.Name),
                "CreatedDate" => students.OrderBy(s => s.CreatedDate).ThenBy(s => s.ID),
                "createdDate_desc" => students.OrderByDescending(s => s.CreatedDate).ThenByDescending(s => s.ID),
                _ => students.OrderBy(s => s.Name).ThenBy(s => s.ID)
            };

            // Total count for pagination UI
            var total = await students.CountAsync(ct);

            // Project exactly the columns needed + first phone/email via correlated subqueries
            var pageItems = await students
                .Select(s => new StudentListItemVM
                {
                    ID = s.ID,
                    Name = s.Name,
                    ParentOrEmployer = s.ParentOrEmployer,
                    Phone = s.Contacts
                        .Where(c => c.Type != null && c.Type.ToLower() == "phone")
                        .Select(c => c.Value)
                        .FirstOrDefault(),
                    Email = s.Contacts
                        .Where(c => c.Type != null && c.Type.ToLower() == "email")
                        .Select(c => c.Value)
                        .FirstOrDefault(),
                    CreatedDate = s.CreatedDate,
                    // Avoid building a whole dictionary: compute the flag per row
                    HasPasswordReset = _context.User
                        .Where(u => u.ID == s.ID)
                        .Select(u => u.PaswordSetDate != null)
                        .FirstOrDefault()
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // ViewBag for sort/search/paging; or return a PagedResult<StudentListItemVM>
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Query = q;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(new PagedResult<StudentListItemVM>
            {
                Items = pageItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            });
        }

        public sealed class StudentListItemVM
        {
            public Guid ID { get; set; }
            public string Name { get; set; } = "";
            public string ParentOrEmployer { get; set; } = "";
            public string? Phone { get; set; }
            public string? Email { get; set; }
            public DateTimeOffset CreatedDate { get; set; }
            public bool HasPasswordReset { get; set; }
        }

        public sealed class PagedResult<T>
        {
            public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalCount { get; set; }
        }

        // GET: Students/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .Include(s => s.Contacts)
                .Include(s => s.Payers)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();

            var user = await _context.User
                .Where(u => u.ID == id)
                .FirstOrDefaultAsync();

            ViewBag.StudentNumber = user.Username;

            ViewBag.PasswordSetDate = user.PaswordSetDate;

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            /*if (!isPrivileged && student.AccountingGroup != "S")
                return Forbid();*/

            return View(student);
        }

        // GET: Students/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .Include(s => s.Contacts)
                .Include(s => s.Payers)
                .FirstOrDefaultAsync(s => s.ID == id);
            if (student == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            /*if (!isPrivileged && student.AccountingGroup != "S")
                return Forbid();*/

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
                ViewBag.ContactTypes = DropdownOptions.ContactTypes;
                ViewBag.SourceTypes = DropdownOptions.SourceTypes;
                ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
                return View(updatedStudent);
            }

            var existingStudent = await _context.Student
                .Include(s => s.Contacts)
                .Include(s => s.Payers)
                .FirstOrDefaultAsync(s => s.ID == id);

            if (existingStudent == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            /*if (!isPrivileged && existingStudent.AccountingGroup != "S")
                return Forbid();*/

            // --- scalar updates ---
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            //existingStudent.AccountingGroup = isPrivileged ? updatedStudent.AccountingGroup : "S";
            if (isPrivileged)
            {
                // If the admin didn’t post a value for some reason, keep existing one.
                if (!string.IsNullOrWhiteSpace(updatedStudent.AccountingGroup))
                    existingStudent.AccountingGroup = updatedStudent.AccountingGroup;
                // else keep existingStudent.AccountingGroup as-is
            }

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
                        contact.ID = Guid.NewGuid();
                        contact.StudentID = existingStudent.ID;
                        _context.Entry(contact).State = EntityState.Added;
                        existingStudent.Contacts.Add(contact);
                    }
                    else
                    {
                        var existingContact = existingStudent.Contacts.FirstOrDefault(c => c.ID == contact.ID);
                        if (existingContact != null)
                        {
                            existingContact.Type = contact.Type;
                            existingContact.Value = contact.Value;
                            existingContact.Invitation = contact.Invitation;
                            existingContact.Emergency = contact.Emergency;
                            existingContact.Money = contact.Money;
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

            if (updatedStudent.Payers != null && updatedStudent.Payers.Any())
            {
                // add or update
                foreach (var payer in updatedStudent.Payers)
                {
                    if (payer.Id == Guid.Empty)
                    {
                        payer.Id = Guid.NewGuid();
                        payer.StudentId = existingStudent.ID;
                        _context.Entry(payer).State = EntityState.Added;
                        existingStudent.Payers.Add(payer);
                    }
                    else
                    {
                        var existingPayer = existingStudent.Payers.FirstOrDefault(p => p.Id == payer.Id);
                        if (existingPayer != null)
                            existingPayer.Name = payer.Name;
                    }
                }

                // delete removed
                var postedIds = updatedStudent.Payers
                    .Where(p => p.Id != Guid.Empty)
                    .Select(p => p.Id)
                    .ToList();

                var toRemove = existingStudent.Payers
                    .Where(p => p.Id != Guid.Empty && !postedIds.Contains(p.Id))
                    .ToList();

                foreach (var p in toRemove)
                    _context.Entry(p).State = EntityState.Deleted;
            }

            // --- NEW: create user if missing ---
            bool userExists = await _context.User.AnyAsync(u => u.ID == existingStudent.ID);
            if (!userExists)
            {
                var rnd = new Random();
                var tempPass = rnd.Next(100000, 1000000).ToString();

                PasswordHelper.CreatePasswordHash(tempPass, out byte[] hash, out byte[] salt);

                string candidate;
                bool exists = true;
                int tries = 0;
                const int maxTries = 5000; // Reasonable cap
                HashSet<string> attempted = new HashSet<string>();

                do
                {
                    int usernameSuffix = rnd.Next(1000000, 10000000); // 7-digit number
                    candidate = $"{usernameSuffix}";

                    if (attempted.Contains(candidate))
                    {
                        continue; // skip DB check if already tried this one
                    }

                    attempted.Add(candidate);

                    exists = await _context.User.AnyAsync(u => u.Username == candidate);

                    if (++tries >= maxTries)
                    {
                        ViewBag.Error = "Could not generate a unique username for this user.";
                        break;
                    }

                } while (exists);

                if (!exists)
                {
                    var newUser = new User
                    {
                        ID = existingStudent.ID,
                        Username = candidate,
                        Role = "student",
                        MustChangePassword = true,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        IncorrectAttempts = 0
                    };

                    _context.User.Add(newUser);
                }

                var primaryEmail = updatedStudent.Contacts?
                    .FirstOrDefault(c =>
                        string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase) &&
                        c.Invitation == true)
                    ?.Value;

                //_logger.LogInformation("Would have sent welcome email with the name of {n} to email: {e}", existingStudent.Name, primaryEmail);

                //TODO: Add Password reset button that will send cred email to primary email and add tempdata to copy for admin 
                /*if (string.IsNullOrWhiteSpace(primaryEmail))
                {
                    /*try
                    {
                        var html = $"""
                            <h2>Bienvenue!</h2>
                            <p>Your temporary credentials at <strong>torontofrench.com</strong></p>
                            <p>
                                <strong>Username :</strong> {candidate}<br/>
                                <strong>Temporary Password :</strong> {tempPass}
                            </p>
                            <p>You will need to change your password upon your first login.</p>
                            <p>
                                Click here to log in: 
                                <a href="https://msdocs-core-sql-tsl.azurewebsites.net/" target="_blank" style="color: #1a73e8;">Log in to your account</a>
                            </p>
                            """;

                        string to = "michael.kamochkin@gmail.com";
                        string subject = "Your credentials at torontofrench.com";
                        await _mailer.SendAsync(
                            to: to,
                            subject: subject,
                            htmlBody: html);

                        //await _logHelper.LogMailAsync(to, subject, html);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send welcome e-mail to student {StudentId}", existingStudent.ID);
                    }
                }
                else
                {
                    try
                    {
                        var html = $"""
                            <h2>Bienvenue!</h2>
                            <p>Your temporary credentials at <strong>torontofrench.com</strong></p>
                            <p>
                                <strong>Username :</strong> {candidate}<br/>
                                <strong>Temporary Password :</strong> {tempPass}
                            </p>
                            <p>You will need to change your password upon your first login.</p>
                            <p>
                                Click here to log in: 
                                <a href="https://msdocs-core-sql-tsl.azurewebsites.net/" target="_blank" style="color: #1a73e8;">Log in to your account</a>
                            </p>
                            """;
                        string to = primaryEmail;
                        string subject = "Your credentials at torontofrench.com";
                        await _mailer.SendAsync(
                            to: to,
                            subject: subject,
                            htmlBody: html);

                        //await _logHelper.LogMailAsync(to, subject, html);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send welcome e-mail to student {StudentId}", existingStudent.ID);
                    }
                }*/


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
        [HttpGet]
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
            [Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts,Payers")]
            Student student)
        {
            // 1) Validate your one-time token
            var formToken = Request.Form["FormToken"].ToString();
            var sessionToken = HttpContext.Session.GetString("CreateStudentToken");
            if (formToken != sessionToken)
            {
                // either a repeat-submit or invalid token -> drop it
                return RedirectToAction(nameof(Index));
            }
            // consume it so it can’t be used again
            HttpContext.Session.Remove("CreateStudentToken");

            /*var allNames = await _context.Student.Select(s => s.Name).ToListAsync();

            bool needsWarning = allNames.Contains(student.Name);

            if (needsWarning)
            {
                // carry warning to TempData
                TempData["WarningMessage"] =
                    "A student with a very similar name already exists. Are you sure?";
                //return View(student);
            }*/

            // set created date

            student.TimeZoneId = "America/New_York";

            student.CreatedDate = DateTime.Now;

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            // non-admin/supports get forced into group "S"
            if (!isPrivileged)
            {
                student.AccountingGroup = "S";
            }

            /*_logger.LogInformation("Create POST received {ContactCount} contacts and {PayerCount} payers",
                     student.Contacts?.Count ?? 0,
                     student.Payers?.Count ?? 0);*/

            // require at least one contact
            if (student.Contacts == null || !student.Contacts.Any())
            {
                //ModelState.AddModelError("", "Please add at least one contact.");
            }

            if (ModelState.IsValid)
            {
                // 1) Save the new Student (and its Contacts)
                student.ID = Guid.NewGuid();
                _context.Add(student);
                //await _context.SaveChangesAsync();

                var suffix = string.IsNullOrWhiteSpace(student.ParentOrEmployer)
                   ? ""
                   : " – " + student.ParentOrEmployer;
                var studentNamePlusParent = student.Name + suffix;

                // 2) Create a Group just for this student
                var group = new DotNetCoreSqlDb.Models.Group
                {
                    Id = Guid.NewGuid(),
                    Name = studentNamePlusParent,
                    IsActive = true,
                    IsManualGroup = false

                };
                _context.Group.Add(group);
                //await _context.SaveChangesAsync();

                // 3) Link the student into that group
                var composition = new StudentGroupComposition
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    StudentId = student.ID,
                    UseMyBalance = true      // or whatever default you prefer
                };
                _context.StudentGroupComposition.Add(composition);

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

                /*string candidate;
                bool exists = true;
                int tries = 0;
                const int maxTries = 5000; // Reasonable cap
                HashSet<string> attempted = new HashSet<string>();

                do
                {
                    int usernameSuffix = rnd.Next(1000000, 10000000); // 7-digit number
                    candidate = $"{usernameSuffix}";

                    if (attempted.Contains(candidate))
                    {
                        continue; // skip DB check if already tried this one
                    }

                    attempted.Add(candidate);

                    exists = await _context.User.AnyAsync(u => u.Username == candidate);

                    if (++tries >= maxTries)
                    {
                        ViewBag.Error = "Could not generate a unique username for this user.";
                        break;
                    }

                } while (exists);*/

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
                        ID = student.ID,
                        Username = candidate,
                        Role = "student",
                        MustChangePassword = false,
                        PasswordHash = passwordHash,
                        PasswordSalt = passwordSalt,
                        IncorrectAttempts = 0
                        //PaswordSetDate = DateTime.UtcNow
                    };

                    _context.User.Add(user);
                }

                var primaryEmail = student.Contacts?
                    .FirstOrDefault(c =>
                        string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase) &&
                        c.Invitation == true)
                    ?.Value;


                if (!string.IsNullOrWhiteSpace(primaryEmail))
                {
                    try
                    {
                        var html = $"""
                             <h3>Welcome to TorontoFrench.com!</h3>
                             <p>Your credentials:</p>
                             <p>
                                 <strong>Student Number :</strong> {candidate}<br/><br/>
                                 <strong>Password :</strong> {defaultPassword}
                             </p>
                             <p>
                                 <a href="https://torontofrench.ca/" target="_blank" style="color: #1a73e8;">Click here to log in</a>
                             </p>
                             """;



                        string to = primaryEmail;
                        string subject = "TorontoFrench.com credentials";
                        await _mailer.SendAsync(
                         to: to,
                         //from: "Toronto French",
                         subject: subject,
                         htmlBody: html);

                        //await _logHelper.LogMailAsync(to, subject, html);

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send welcome e-mail to student {StudentId}", student.ID);
                        // swallow → the user is created even if mail fails
                    }
                }
                /*
                else
                {
                    try
                    {
                        var html = $"""
                             <h2>Bienvenue!</h2>
                             <p>Your temporary credentials at <strong>TorontoFrench.com</strong></p>
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

                        await _mailer.SendAsync(
                         to:       primaryEmail,
                         //from: "Toronto French",
                         subject:  "Your credentials at Toronto French",
                         htmlBody: html);
                        string to = "michael.kamochkin@gmail.com";
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
                            "Failed to send welcome e-mail to student {StudentId}", student.ID);
                        // swallow → the user is created even if mail fails
                    }

                }*/


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

                // Save ephemeral credentials (one-time) for UI display after redirect

                try
                {
                    // put creds into TempData just before saving
                    TempData["JustCreatedOrChangedStudentName"] = student.Name;
                    TempData["JustCreatedOrChangedStudentUsername"] = candidate;
                    TempData["JustCreatedOrChangedStudentPassword"] = defaultPassword;

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
            else
            {
                foreach (var kvp in ModelState)
                {
                    var key = kvp.Key;
                    var errors = kvp.Value.Errors;
                    foreach (var err in errors)
                    {
                        _logger.LogWarning("ModelState error for '{Key}': {ErrorMessage}", key, err.ErrorMessage);
                    }
                }

                // if we hit errors, repopulate the dropdowns and show the form again
                ViewBag.ContactTypes = DropdownOptions.ContactTypes;
                ViewBag.SourceTypes = DropdownOptions.SourceTypes;
                ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
                return View(student);
            }


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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeletePayer(Guid payerId)
        {
            var payer = await _context.Payer.FindAsync(payerId);
            if (payer == null)
                return Json(new { success = false, message = "Payer not found." });

            _context.Payer.Remove(payer);
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


        /*[Authorize(Roles = "admin, support, assistant")]
        [HttpGet]
        public async Task<JsonResult> CheckDuplicateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { duplicate = false, closeMatches = new string[0] });

            // Pull all names into memory (can optimize later with indexed subset if needed)
            var allNames = await _context.Student
                                .Select(s => s.Name)
                                .ToListAsync();

            // Do fuzzy comparison in C#
            var threshold = 75;  // adjust as needed
            var matches = allNames
                .Select(existing => new
                {
                    Name = existing,
                    Score = Fuzz.TokenSetRatio(existing, name)
                })
                .Where(x => x.Score >= threshold)
                .OrderByDescending(x => x.Score)
                .Take(5)
                .Select(x => x.Name)
                .ToList();

            return Json(new
            {
                duplicate = matches.Any(),
                closeMatches = matches
            });
        }*/

        [Authorize(Roles = "admin, support, assistant")]
        [HttpGet]
        public async Task<JsonResult> CheckDuplicatePhone(string value)
        {
            //_logger.LogInformation("In check duplicate phone with phone: " + value);

            if (string.IsNullOrWhiteSpace(value) || value.Length < 6)
                return Json(new { duplicate = false, closeMatches = new string[0] });

            // Clean input phone
            string cleanedInput = Regex.Replace(value, @"\D", "");
            if (cleanedInput.Length < 6)
                return Json(new { duplicate = false, closeMatches = new string[0] });

            string lastSixOfPhone = cleanedInput.Substring(cleanedInput.Length - 6);

            var allPhonesRaw = await _context.Contact
                .Where(c => c.Type == "Phone")
                .Select(s => s.Value)
                .ToListAsync();

            var matches = new List<string>();

            foreach (var raw in allPhonesRaw)
            {
                string cleaned = Regex.Replace(raw ?? "", @"\D", "");
                if (cleaned.Length >= 6)
                {
                    string lastSix = cleaned.Substring(cleaned.Length - 6);
                    //_logger.LogInformation($"Comparing {lastSix} with {lastSixOfPhone}");

                    if (lastSix == lastSixOfPhone)
                    {
                        matches.Add(cleaned); // or add raw if you want original format
                    }
                }
            }

            return Json(new
            {
                duplicate = matches.Any(),
                closeMatches = matches
            });
        }

        [Authorize(Roles = "admin, support, assistant")]
        [HttpGet]
        public async Task<JsonResult> CheckDuplicateEmail(string value)
        {
            //_logger.LogInformation("In check duplicate phone with phone: " + value);

            if (string.IsNullOrWhiteSpace(value) || value.Length < 6)
                return Json(new { duplicate = false, closeMatches = new string[0] });

            // Clean input phone
            string cleanedInput = Regex.Replace(value, "[^a-zA-Z0-9]", "").ToString().ToLower();

            var allEmailsRaw = await _context.Contact
                .Where(c => c.Type == "Email")
                .Select(s => s.Value)
                .ToListAsync();

            var matches = new List<string>();

            foreach (var raw in allEmailsRaw)
            {
                string cleaned = Regex.Replace(raw, "[^a-zA-Z0-9]", "").ToLower();

                if (cleaned == cleanedInput)
                {
                    matches.Add(raw);
                }
            }

            return Json(new
            {
                duplicate = matches.Any(),
                closeMatches = matches
            });
        }

        [Authorize(Roles = "admin, support, assistant")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid studentId)
        {
            // 1) Load student & enforce same access rules you use elsewhere
            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == studentId);

            if (student == null)
                return NotFound();

            // 2) Load the linked User row
            var user = await _context.User.FirstOrDefaultAsync(u => u.ID == studentId);
            if (user == null)
                return NotFound(); // or create one if you prefer

            Random rnd = new Random();
            string newPassword = rnd.Next(100000, 1000000) + "";

            PasswordHelper.CreatePasswordHash(
                newPassword,
                out byte[] passwordHash,
                out byte[] passwordSalt
            );


            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            user.MustChangePassword = false;
            user.IncorrectAttempts = 0;
            user.PaswordSetDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // 5) Put plaintext creds into TempData for one-time display
            TempData["JustCreatedOrChangedStudentName"] = student.Name;
            TempData["JustCreatedOrChangedStudentUsername"] = user.Username;
            TempData["JustCreatedOrChangedStudentPassword"] = newPassword;

            //_logger.LogInformation("changed password: " + newPassword);

            // 6) Redirect back to Edit (or wherever you prefer to show the TempData)
            return RedirectToAction(nameof(Index));
        }

    }
}