using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

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
            // Include all related data in one query
            IQueryable<Student> query = _context.Student
                .Include(s => s.Contacts)
                .Include(s => s.Notes);
            
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
                .Include(s => s.Notes)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();
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
                .Include(s => s.Notes)
                .FirstOrDefaultAsync(s => s.ID == id);
            if (student == null)
                return NotFound();
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
            if (!User.IsInRole("admin") && existingStudent.AccountingGroup != "S")
                return Forbid();

            // Update scalar properties.
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            existingStudent.AccountingGroup = User.IsInRole("admin") ? updatedStudent.AccountingGroup : "S";

            // Handle contacts
            if (updatedStudent.Contacts != null)
            {
                // Remove contacts that are no longer present
                var existingContactIds = existingStudent.Contacts.Select(c => c.ID).ToList();
                var updatedContactIds = updatedStudent.Contacts.Select(c => c.ID).ToList();
                var contactsToRemove = existingStudent.Contacts.Where(c => !updatedContactIds.Contains(c.ID)).ToList();
                foreach (var contact in contactsToRemove)
                {
                    _context.Contact.Remove(contact);
                }

                // Update or add contacts
                foreach (var contact in updatedStudent.Contacts)
                {
                    if (contact.ID == Guid.Empty)
                    {
                        // New contact
                        contact.StudentID = existingStudent.ID;
                        _context.Contact.Add(contact);
                    }
                    else
                    {
                        // Existing contact
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
            }

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
            if (User.IsInRole("admin"))
            {
                student.AccountingGroup = "A";
            }
            return View(student);
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")] Student student)
        {
            if (ModelState.IsValid)
            {
                student.ID = Guid.NewGuid();
                student.CreatedDate = DateTime.Now;
                
                // Set AccountingGroup based on user role
                if (!User.IsInRole("admin"))
                {
                    student.AccountingGroup = "S";
                }

                // Initialize contacts collection if null
                if (student.Contacts == null)
                {
                    student.Contacts = new List<Contact>();
                }

                // Set StudentID for each contact and ensure they have IDs
                foreach (var contact in student.Contacts)
                {
                    contact.ID = Guid.NewGuid();
                    contact.StudentID = student.ID;
                }

                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // If we get here, something went wrong
            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
            return View(student);
        }

        // Delete actions commented out for Students...

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
