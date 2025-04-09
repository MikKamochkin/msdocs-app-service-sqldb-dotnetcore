using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "admin")]
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
        public async Task<IActionResult> Create([Bind("Name,StudentGroupCompositions")] Group group)
        {
            if (ModelState.IsValid)
            {
                // Generate a new ID for the group
                group.Id = Guid.NewGuid();
                
                // Clear any existing StudentGroupCompositions to avoid duplicates
                group.StudentGroupCompositions = new List<StudentGroupComposition>();

                // Process the form data to extract StudentGroupCompositions
                var form = await HttpContext.Request.ReadFormAsync();
                var studentIds = form.Keys.Where(k => k.StartsWith("StudentGroupCompositions.StudentId")).ToList();
                var useMyBalanceValues = form.Keys.Where(k => k.StartsWith("StudentGroupCompositions.UseMyBalance")).ToList();

                // Log the form data for debugging
                System.Diagnostics.Debug.WriteLine($"Found {studentIds.Count} student IDs in form data");
                foreach (var key in studentIds)
                {
                    System.Diagnostics.Debug.WriteLine($"Student ID key: {key}, Value: {form[key]}");
                }

                // Create a dictionary to match StudentIds with their corresponding UseMyBalance values
                var studentCompositions = new Dictionary<string, bool>();
                foreach (var key in studentIds)
                {
                    var studentId = form[key].ToString();
                    var useMyBalanceKey = key.Replace("StudentId", "UseMyBalance");
                    var useMyBalance = form.ContainsKey(useMyBalanceKey) && form[useMyBalanceKey].ToString().ToLower() == "true";
                    studentCompositions[studentId] = useMyBalance;
                    System.Diagnostics.Debug.WriteLine($"Added student {studentId} with UseMyBalance={useMyBalance}");
                }

                // Add each student to the group's StudentGroupCompositions
                foreach (var studentId in studentCompositions.Keys)
                {
                    if (Guid.TryParse(studentId, out Guid id))
                    {
                        var composition = new StudentGroupComposition
                        {
                            Id = Guid.NewGuid(),
                            GroupId = group.Id,
                            StudentId = id,
                            UseMyBalance = studentCompositions[studentId]
                        };
                        group.StudentGroupCompositions.Add(composition);
                        System.Diagnostics.Debug.WriteLine($"Added StudentGroupComposition: Id={composition.Id}, GroupId={composition.GroupId}, StudentId={id}, UseMyBalance={studentCompositions[studentId]}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to parse student ID: {studentId}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Group has {group.StudentGroupCompositions.Count} StudentGroupCompositions before saving");
                
                _context.Add(group);
                await _context.SaveChangesAsync();
                
                // Verify that the StudentGroupCompositions were saved
                var savedGroup = await _context.Group
                    .Include(g => g.StudentGroupCompositions)
                    .FirstOrDefaultAsync(g => g.Id == group.Id);
                
                if (savedGroup != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Saved group has {savedGroup.StudentGroupCompositions.Count} StudentGroupCompositions");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to retrieve saved group");
                }
                
                return RedirectToAction(nameof(Index));
            }
            
            // Log validation errors
            foreach (var modelState in ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    System.Diagnostics.Debug.WriteLine($"Validation error: {error.ErrorMessage}");
                }
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