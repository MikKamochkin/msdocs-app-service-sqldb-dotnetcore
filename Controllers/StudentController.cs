using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Controllers
{
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

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
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
            // Prepare a dropdown list for contact types
            ViewBag.ContactTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Email", Value = "Email" },
                new SelectListItem { Text = "Phone", Value = "Phone" },
                new SelectListItem { Text = "Facebook", Value = "Facebook" },
                new SelectListItem { Text = "Instagram", Value = "Instagram" },
                new SelectListItem { Text = "Telegram", Value = "Telegram" },
                new SelectListItem { Text = "Twitter", Value = "Twitter" }
            };
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ParentOrEmployer,MainNotes,OngoingNotes,CreatedDate,Contacts")] Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // If model state is invalid, repopulate the contact types
            ViewBag.ContactTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Email", Value = "Email" },
                new SelectListItem { Text = "Phone", Value = "Phone" },
                new SelectListItem { Text = "Facebook", Value = "Facebook" },
                new SelectListItem { Text = "Instagram", Value = "Instagram" },
                new SelectListItem { Text = "Telegram", Value = "Telegram" },
                new SelectListItem { Text = "Twitter", Value = "Twitter" }
            };
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID == id);
            if (student == null)
                return NotFound();

            ViewBag.ContactTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Email", Value = "Email" },
                new SelectListItem { Text = "Phone", Value = "Phone" },
                new SelectListItem { Text = "Facebook", Value = "Facebook" },
                new SelectListItem { Text = "Instagram", Value = "Instagram" },
                new SelectListItem { Text = "Telegram", Value = "Telegram" },
                new SelectListItem { Text = "Twitter", Value = "Twitter" }
            };

            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,ParentOrEmployer,MainNotes,OngoingNotes,CreatedDate,Contacts")] Student student)
        {
            if (id != student.ID)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Student.Any(e => e.ID == student.ID))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ContactTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Email", Value = "Email" },
                new SelectListItem { Text = "Phone", Value = "Phone" },
                new SelectListItem { Text = "Facebook", Value = "Facebook" },
                new SelectListItem { Text = "Instagram", Value = "Instagram" },
                new SelectListItem { Text = "Telegram", Value = "Telegram" },
                new SelectListItem { Text = "Twitter", Value = "Twitter" }
            };

            return View(student);
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var student = await _context.Student
                .FirstOrDefaultAsync(m => m.ID == id);
            if (student == null)
                return NotFound();

            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
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
