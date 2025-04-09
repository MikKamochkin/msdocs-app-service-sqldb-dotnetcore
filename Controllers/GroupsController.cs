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
        public async Task<IActionResult> Create([Bind("Name")] Group group)
        {
            if (ModelState.IsValid)
            {
                // Generate a new ID for the group
                group.Id = Guid.NewGuid();
                
                // Add the group to the context first
                _context.Add(group);
                
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
                        
                        // Explicitly add each StudentGroupComposition to the context
                        _context.StudentGroupComposition.Add(composition);
                        System.Diagnostics.Debug.WriteLine($"Added StudentGroupComposition: Id={composition.Id}, GroupId={composition.GroupId}, StudentId={id}, UseMyBalance={studentCompositions[studentId]}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to parse student ID: {studentId}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Group has {studentCompositions.Count} StudentGroupCompositions before saving");
                
                // Save all changes to the database
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
                    // Get the existing group with its compositions
                    var existingGroup = await _context.Group
                        .Include(g => g.StudentGroupCompositions)
                        .FirstOrDefaultAsync(g => g.Id == id);
                    
                    if (existingGroup == null)
                        return NotFound();
                    
                    // Update the group name
                    existingGroup.Name = group.Name;
                    
                    // Process the form data to extract StudentGroupCompositions
                    var form = await HttpContext.Request.ReadFormAsync();
                    var studentIds = form.Keys.Where(k => k.StartsWith("StudentGroupCompositions.StudentId")).ToList();
                    var useMyBalanceValues = form.Keys.Where(k => k.StartsWith("StudentGroupCompositions.UseMyBalance")).ToList();
                    
                    // Create a dictionary to match StudentIds with their corresponding UseMyBalance values
                    var studentCompositions = new Dictionary<string, bool>();
                    foreach (var key in studentIds)
                    {
                        var studentId = form[key].ToString();
                        var useMyBalanceKey = key.Replace("StudentId", "UseMyBalance");
                        var useMyBalance = form.ContainsKey(useMyBalanceKey) && form[useMyBalanceKey].ToString().ToLower() == "true";
                        studentCompositions[studentId] = useMyBalance;
                    }
                    
                    // Remove compositions that are no longer in the form
                    var compositionsToRemove = existingGroup.StudentGroupCompositions
                        .Where(c => !studentCompositions.ContainsKey(c.StudentId.ToString()))
                        .ToList();
                    
                    foreach (var composition in compositionsToRemove)
                    {
                        _context.StudentGroupComposition.Remove(composition);
                    }
                    
                    // Add or update compositions
                    foreach (var studentId in studentCompositions.Keys)
                    {
                        if (Guid.TryParse(studentId, out Guid studentGuid))
                        {
                            var existingComposition = existingGroup.StudentGroupCompositions
                                .FirstOrDefault(c => c.StudentId == studentGuid);
                            
                            if (existingComposition != null)
                            {
                                // Update existing composition
                                existingComposition.UseMyBalance = studentCompositions[studentId];
                            }
                            else
                            {
                                // Add new composition
                                var newComposition = new StudentGroupComposition
                                {
                                    Id = Guid.NewGuid(),
                                    GroupId = existingGroup.Id,
                                    StudentId = studentGuid,
                                    UseMyBalance = studentCompositions[studentId]
                                };
                                _context.StudentGroupComposition.Add(newComposition);
                            }
                        }
                    }
                    
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

        // POST: Groups/DeleteStudentGroupComposition
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
    }
} 