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
    public class StudentsController : Controller
    {
        private readonly MyDatabaseContext _context;

        public StudentsController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Students
        public async Task<IActionResult> Index(string sortOrder)
        {
            ViewBag.CurrentSort = sortOrder;
            // Include Contacts so that we can display email addresses.
            IQueryable<Student> query = _context.Student.Include(s => s.Contacts);

            // If non-admin, filter by AccountingGroup "S"
            if (!User.IsInRole("admin"))
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

            // Non-admin users can only see students with AccountingGroup "S"
            if (!User.IsInRole("admin") && student.AccountingGroup != "S")
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

            // Non-admin users can only edit students with AccountingGroup "S"
            if (!User.IsInRole("admin") && student.AccountingGroup != "S")
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
                // Repopulate dropdowns if invalid.
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

            // Non-admin users can only edit students with AccountingGroup "S"
            if (!User.IsInRole("admin") && existingStudent.AccountingGroup != "S")
                return Forbid();

            // Update scalar properties.
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            // Force non-admin users to keep AccountingGroup "S"
            existingStudent.AccountingGroup = User.IsInRole("admin") ? updatedStudent.AccountingGroup : "S";

            // If contacts were posted, update and add new ones.
            if (updatedStudent.Contacts != null)
            {
                // Process each posted contact.
                foreach (var contact in updatedStudent.Contacts)
                {
                    // If ID is the default GUID, it's a new contact.
                    if (contact.ID == Guid.Empty)
                    {
                        contact.ID = Guid.NewGuid();
                        contact.StudentID = existingStudent.ID;
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

                // Remove any contacts that exist in the DB but weren't posted.
                var postedContactIds = updatedStudent.Contacts
                                            .Where(c => c.ID != Guid.Empty)
                                            .Select(c => c.ID)
                                            .ToList();

                // Make a copy of existing contacts that are not new.
                var contactsToRemove = existingStudent.Contacts
                                        .Where(c => c.ID != Guid.Empty && !postedContactIds.Contains(c.ID))
                                        .ToList();
                foreach (var c in contactsToRemove)
                {
                    _context.Entry(c).State = EntityState.Deleted;
                }
            }
            // If no contacts were submitted, leave the existing contacts unchanged.

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

        /*
        // GET: Students/Delete/{id}
        public async Task<IActionResult> Delete(Guid id)
        {
            var student = await _context.Student.FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();

            // Non-admin users can only delete students with AccountingGroup "S"
            if (!User.IsInRole("admin") && student.AccountingGroup != "S")
                return Forbid();

            return View(student);
        }

        // POST: Students/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student != null)
            {
                // Non-admin users can only delete students with AccountingGroup "S"
                if (!User.IsInRole("admin") && student.AccountingGroup != "S")
                    return Forbid();

                _context.Student.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        */

       // GET: Students/Create
        public IActionResult Create()
        {
            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();

            // For admin users, default the AccountingGroup to "A".
            // Also, initialize the required property 'Name' to an empty string.
            Student student = new Student { Name = string.Empty };
            if (User.IsInRole("admin"))
            {
                student.AccountingGroup = "A";
            }
            return View(student);
        }


        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")] Student student)
        {
            // Set the CreatedDate automatically to the current time.
            student.CreatedDate = DateTime.Now;

            // If the current user is not an admin, force AccountingGroup to "S".
            if (!User.IsInRole("admin"))
            {
                student.AccountingGroup = "S";
            }

            // Server-side validation: Ensure at least one contact is added.
            if (student.Contacts == null || !student.Contacts.Any())
            {
                ModelState.AddModelError("", "Please add at least one contact.");
            }

            if (ModelState.IsValid)
            {
                // Generate a new GUID for the student's ID.
                student.ID = Guid.NewGuid();
                
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Repopulate dropdown lists if model state is invalid.
            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();

            return View(student);
        }
    }
}