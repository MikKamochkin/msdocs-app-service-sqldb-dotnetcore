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

            ViewBag.SourceTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Google Khinich School", Value = "Google Khinich School"},
                new SelectListItem { Text = "Google Toronto French", Value = "Google Toronto French"},
                new SelectListItem { Text = "Facebok Khinich School", Value = "Facebok Khinich School"},
                new SelectListItem { Text = "Facebook Toronto French", Value = "Facebook Toronto French"},
                new SelectListItem { Text = "Instagram Khinich School", Value = "Instagram Khinich School"},
                new SelectListItem { Text = "Instagram Toronto French", Value = "Instagram Toronto French"},
                new SelectListItem { Text = "Referall from Toronto French", Value = "Referall from Toronto French"},
                new SelectListItem { Text = "Referall from Khinich School", Value = "Referall from Khinich School"},
                new SelectListItem { Text = "DM in Whatsapp Toronto French", Value = "DM in Whatsapp Toronto French"},
                new SelectListItem { Text = "DM in Whatsapp Khinich School", Value = "DM in Whatsapp Khinich School"},
                new SelectListItem { Text = "Phone Call from Toronto French Site", Value = "Phone Call from Toronto French Site"},
                new SelectListItem { Text = "Phone Call from Toronto French by Location", Value = "Phone Call from Toronto French by Location"}
            };

            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,ParentOrEmployer,MainNotes,CreatedDate,Source,Contacts")] Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // Repopulate dropdown lists if the model state is invalid.
            ViewBag.ContactTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Email", Value = "Email" },
                new SelectListItem { Text = "Phone", Value = "Phone" },
                new SelectListItem { Text = "Facebook", Value = "Facebook" },
                new SelectListItem { Text = "Instagram", Value = "Instagram" },
                new SelectListItem { Text = "Telegram", Value = "Telegram" },
                new SelectListItem { Text = "Twitter", Value = "Twitter" }
            };

            ViewBag.SourceTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Google Khinich School", Value = "Google Khinich School"},
                new SelectListItem { Text = "Google Toronto French", Value = "Google Toronto French"},
                new SelectListItem { Text = "Facebok Khinich School", Value = "Facebok Khinich School"},
                new SelectListItem { Text = "Facebook Toronto French", Value = "Facebook Toronto French"},
                new SelectListItem { Text = "Instagram Khinich School", Value = "Instagram Khinich School"},
                new SelectListItem { Text = "Instagram Toronto French", Value = "Instagram Toronto French"},
                new SelectListItem { Text = "Referall from Toronto French", Value = "Referall from Toronto French"},
                new SelectListItem { Text = "Referall from Khinich School", Value = "Referall from Khinich School"},
                new SelectListItem { Text = "DM in Whatsapp Toronto French", Value = "DM in Whatsapp Toronto French"},
                new SelectListItem { Text = "DM in Whatsapp Khinich School", Value = "DM in Whatsapp Khinich School"},
                new SelectListItem { Text = "Phone Call from Toronto French Site", Value = "Phone Call from Toronto French Site"},
                new SelectListItem { Text = "Phone Call from Toronto French by Location", Value = "Phone Call from Toronto French by Location"}
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
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,ParentOrEmployer,MainNotes,CreatedDate,Contacts")] Student student)
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
