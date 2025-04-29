using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize]
    public class StudentManagementController : Controller
    {
        private readonly MyDatabaseContext _context;
        public StudentManagementController(MyDatabaseContext context)
        {
            _context = context;
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
                ViewBag.ContactTypes = DropdownOptions.ContactTypes;
                ViewBag.SourceTypes = DropdownOptions.SourceTypes;
                ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
                ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
                return View(updatedStudent);
            }

            // Load existing student (with contacts) from DB.
            var existingStudent = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == id);
            if (existingStudent == null)
                return NotFound();

            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");
            if (!isPrivileged && existingStudent.AccountingGroup != "S")
                return Forbid();

            // Update scalar properties.
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            existingStudent.AccountingGroup = isPrivileged ? updatedStudent.AccountingGroup : "S";


            var suffix   = string.IsNullOrWhiteSpace(existingStudent.ParentOrEmployer)
                   ? ""
                   : " – " + existingStudent.ParentOrEmployer;
            var studentNamePlusParent = existingStudent.Name + suffix;


            // Find all the GroupIds where this student appears
            var soloGroupIds = await _context.StudentGroupComposition
                .Where(sgc => sgc.StudentId == id)          // all compositions for this student
                .GroupBy(sgc => sgc.GroupId)                // group them by GroupId
                .Where(g => g.Count() == 1)                 // keep only groups with exactly one member
                .Select(g => g.Key)                         // select the GroupId
                .ToListAsync();

            // 2) Load those groups
            var soloGroups = await _context.Group
                .Where(g => soloGroupIds.Contains(g.Id))
                .ToListAsync();

            // 3) Rename them
            foreach (var g in soloGroups)
                g.Name = studentNamePlusParent;

            // Synchronize the Contacts collection.
            if (updatedStudent.Contacts != null && updatedStudent.Contacts.Any())
            {
                // Process each posted contact.
                foreach (var contact in updatedStudent.Contacts)
                {
                    // Treat as new if ID is Guid.Empty or equals the all-zero string.
                    if (contact.ID == Guid.Empty || contact.ID.ToString() == "00000000-0000-0000-0000-000000000000")
                    {
                        // Assign new ID and mark as Added.
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
                // Remove any contacts that were removed on the UI.
                var postedContactIds = updatedStudent.Contacts
                                        .Where(c => c.ID != Guid.Empty && c.ID.ToString() != "00000000-0000-0000-0000-000000000000")
                                        .Select(c => c.ID)
                                        .ToList();
                var contactsToRemove = existingStudent.Contacts
                                        .Where(c => c.ID != Guid.Empty && !postedContactIds.Contains(c.ID))
                                        .ToList();
                foreach (var c in contactsToRemove)
                {
                    _context.Entry(c).State = EntityState.Deleted;
                }
            }
            // If no contacts were submitted, leave existing contacts unchanged.

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
            return View(student);
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")]
            Student student)
        {
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
                await _context.SaveChangesAsync();

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
                await _context.SaveChangesAsync();

                // 3) Link the student into that group
                var composition = new StudentGroupComposition
                {
                    Id           = Guid.NewGuid(),
                    GroupId      = group.Id,
                    StudentId    = student.ID,
                    UseMyBalance = true      // or whatever default you prefer
                };
                _context.StudentGroupComposition.Add(composition);

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

                // 5) Persist composition + assignment
                await _context.SaveChangesAsync();*/

                return RedirectToAction(nameof(Index));
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
