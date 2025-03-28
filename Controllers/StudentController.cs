using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
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
        public async Task<IActionResult> Index()
        {
            return View(await _context.Student.ToListAsync());
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

            return View(student);
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            //for future use: var tz = TimeZoneInfo.FindSystemTimeZoneById(yourRecord.TimeZoneId);
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();
            /*
            depricated version of time zones:
             ViewBag.TimeZones = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => new SelectListItem { Value = tz.Id, Text = tz.DisplayName })
                .ToList();
            */

            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")] Student student)
        {
            // Set the CreatedDate automatically to the current time.
            student.CreatedDate = DateTime.Now;

            // If the current user is not an admin, ignore any submitted AccountingGroup value.
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

            ViewBag.ContactTypes = DropdownOptions.ContactTypes;
            ViewBag.SourceTypes = DropdownOptions.SourceTypes;
            ViewBag.AccountingGroupTypes = DropdownOptions.AccountingGroupTypes;
            ViewBag.Timezones = TimeZoneMapping.GetTimeZones();

            return View(student);
        }

        // POST: Students/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ID,Name,ParentOrEmployer,MainNotes,Source,TimeZoneId,AccountingGroup,Contacts")] Student updatedStudent)
        {
            if (id != updatedStudent.ID)
                return NotFound();

            if (!ModelState.IsValid)
            {
                // Repopulate dropdowns if necessary.
                return View(updatedStudent);
            }

            // Load the existing student with its contacts from the database.
            var existingStudent = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == id);

            if (existingStudent == null)
                return NotFound();

            // Update the scalar properties.
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            existingStudent.AccountingGroup = updatedStudent.AccountingGroup;

            // Update the contacts collection.
            foreach (var updatedContact in updatedStudent.Contacts)
            {
                // Find the existing contact by ID.
                var existingContact = existingStudent.Contacts.FirstOrDefault(c => c.ID == updatedContact.ID);

                if (existingContact != null)
                {
                    // Update the existing contact's properties.
                    existingContact.Type = updatedContact.Type;
                    existingContact.Value = updatedContact.Value;
                    existingContact.Invitation = updatedContact.Invitation;
                    existingContact.Emergency = updatedContact.Emergency;
                    existingContact.Money = updatedContact.Money;
                }
                else
                {
                    // If the contact does not exist, it may be a new contact.
                    // You can add it if that's expected.
                    existingStudent.Contacts.Add(updatedContact);
                }
            }

            // Optionally, if you want to remove contacts that were deleted in the form,
            // compare existingStudent.Contacts with updatedStudent.Contacts and remove missing ones.

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


        // GET: Students/Delete/{id}
        public async Task<IActionResult> Delete(Guid id)
        {
            var student = await _context.Student
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();

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
                _context.Student.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
