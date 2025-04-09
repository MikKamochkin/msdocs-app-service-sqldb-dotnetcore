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
    [Authorize(Roles = "Admin")]
    public class GroupsController : Controller
    {
        private readonly MyDatabaseContext _context;

        public GroupsController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Groups
        public async Task<IActionResult> Index()
        {
            var groups = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                    .ThenInclude(sgc => sgc.Student)
                .ToListAsync();
            return View(groups);
        }

        // GET: Groups/Create
        public IActionResult Create()
        {
            ViewBag.Students = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Student.OrderBy(s => s.Name), "ID", "Name");
            return View(new Group { Name = "" });
        }

        // POST: Groups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Group group)
        {
            if (ModelState.IsValid)
            {
                group.Id = Guid.NewGuid();
                _context.Add(group);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Students = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Student.OrderBy(s => s.Name), "ID", "Name");
            return View(group);
        }

        // GET: Groups/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return NotFound();

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
                return NotFound();

            ViewBag.Students = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Student.OrderBy(s => s.Name), "ID", "Name");
            return View(group);
        }

        // POST: Groups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Group group)
        {
            if (id != group.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(group);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Group.Any(e => e.Id == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Students = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Student.OrderBy(s => s.Name), "ID", "Name");
            return View(group);
        }

        /*// POST: Groups/DeleteStudentGroupComposition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudentGroupComposition(Guid compositionId)
        {
            var composition = await _context.StudentGroupComposition.FindAsync(compositionId);
            if (composition != null)
            {
                _context.StudentGroupComposition.Remove(composition);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
        */
    }
} 