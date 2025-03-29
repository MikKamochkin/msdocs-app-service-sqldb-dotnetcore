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

            // Update scalar properties.
            existingStudent.Name = updatedStudent.Name;
            existingStudent.ParentOrEmployer = updatedStudent.ParentOrEmployer;
            existingStudent.MainNotes = updatedStudent.MainNotes;
            existingStudent.Source = updatedStudent.Source;
            existingStudent.TimeZoneId = updatedStudent.TimeZoneId;
            existingStudent.AccountingGroup = updatedStudent.AccountingGroup;

            // Update contacts.
            foreach (var contact in updatedStudent.Contacts)
            {
                // If ID is the default GUID, it is a new contact.
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

            // (Optional: If deletion is desired, remove contacts that are missing from updatedStudent.Contacts)

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