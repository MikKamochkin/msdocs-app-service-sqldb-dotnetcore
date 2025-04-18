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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "admin")]
    public class GroupsController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(MyDatabaseContext context, ILogger<GroupsController> logger)
        {
            _context = context;
            _logger = logger;
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

        // GET: Groups/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .ThenInclude(sgc => sgc.Student)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (group == null)
            {
                return NotFound();
            }

            return View(group);
        }

        // GET: Groups/Create
        public IActionResult Create()
        {
            try
            {
                // Get all students for the dropdown
                var students = _context.Student.OrderBy(s => s.Name).ToList();
                if (students != null && students.Any())
                {
                    // Ensure we only include students with valid ID and Name
                    var validStudents = students.Where(s => s.ID != Guid.Empty && !string.IsNullOrEmpty(s.Name)).ToList();
                    if (validStudents.Any())
                    {
                        ViewBag.Students = new SelectList(validStudents, "ID", "Name");
                    }
                    else
                    {
                        ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                        _logger.LogWarning("No valid students found in the database");
                    }
                }
                else
                {
                    // If no students exist, create an empty list
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                    _logger.LogWarning("No students found in the database");
                }
                
                return View(new Group { Name = "" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Create GET action");
                ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                return View(new Group { Name = "" });
            }
        }

        // POST: Groups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Group group)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Create the group first
                    group.Id = Guid.NewGuid();
                    _context.Add(group);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"Group created with ID: {group.Id}");
                    
                    // Now handle the student associations
                    var studentIds = Request.Form["StudentIds"].ToString().Split(',');
                    var useMyBalanceValues = Request.Form["UseMyBalanceValues"].ToString().Split(',');
                    
                    _logger.LogInformation($"Found {studentIds.Length} student IDs");
                    
                    // Process each student
                    for (int i = 0; i < studentIds.Length; i++)
                    {
                        if (Guid.TryParse(studentIds[i], out Guid studentId))
                        {
                            bool useMyBalance = i < useMyBalanceValues.Length && useMyBalanceValues[i].ToLower() == "true";
                            
                            _logger.LogInformation($"Processing student {studentId} with UseMyBalance: {useMyBalance}");
                            
                            var composition = new StudentGroupComposition
                            {
                                Id = Guid.NewGuid(),
                                GroupId = group.Id,
                                StudentId = studentId,
                                UseMyBalance = useMyBalance
                            };
                            
                            _context.StudentGroupComposition.Add(composition);
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Create POST action");
                    ModelState.AddModelError("", "An error occurred while creating the group. Please try again.");
                }
            }
            
            // If we get here, something went wrong
            try
            {
                var students = _context.Student.OrderBy(s => s.Name).ToList();
                if (students != null && students.Any())
                {
                    ViewBag.Students = new SelectList(students, "ID", "Name");
                }
                else
                {
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students in Create POST action");
                ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            
            return View(group);
        }

        // GET: Groups/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (group == null)
            {
                return NotFound();
            }
            
            // Get all students for the dropdown
            try
            {
                var students = _context.Student.OrderBy(s => s.Name).ToList();
                if (students != null && students.Any())
                {
                    ViewBag.Students = new SelectList(students, "ID", "Name");
                }
                else
                {
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students in Edit GET action");
                ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            
            return View(group);
        }

        // POST: Groups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] Group group)
        {
            if (id != group.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update the group name
                    _context.Update(group);
                    
                    // Handle student associations
                    var existingCompositions = await _context.StudentGroupComposition
                        .Where(sgc => sgc.GroupId == id)
                        .ToListAsync();
                    
                    // Remove existing compositions
                    _context.StudentGroupComposition.RemoveRange(existingCompositions);
                    
                    // Add new compositions
                    var studentIds = Request.Form["StudentIds"].ToString().Split(',');
                    var useMyBalanceValues = Request.Form["UseMyBalanceValues"].ToString().Split(',');
                    
                    for (int i = 0; i < studentIds.Length; i++)
                    {
                        if (Guid.TryParse(studentIds[i], out Guid studentId))
                        {
                            bool useMyBalance = i < useMyBalanceValues.Length && useMyBalanceValues[i].ToLower() == "true";
                            
                            var composition = new StudentGroupComposition
                            {
                                Id = Guid.NewGuid(),
                                GroupId = id,
                                StudentId = studentId,
                                UseMyBalance = useMyBalance
                            };
                            
                            _context.StudentGroupComposition.Add(composition);
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GroupExists(group.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            // If we get here, something went wrong
            try
            {
                var students = _context.Student.OrderBy(s => s.Name).ToList();
                if (students != null && students.Any())
                {
                    ViewBag.Students = new SelectList(students, "ID", "Name");
                }
                else
                {
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading students in Edit POST action");
                ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            
            return View(group);
        }

        // GET: Groups/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .ThenInclude(sgc => sgc.Student)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (group == null)
            {
                return NotFound();
            }

            return View(group);
        }

        // POST: Groups/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (group != null)
            {
                // Remove all student compositions first
                _context.StudentGroupComposition.RemoveRange(group.StudentGroupCompositions);
                
                // Then remove the group
                _context.Group.Remove(group);
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction(nameof(Index));
        }

        private bool GroupExists(Guid id)
        {
            return _context.Group.Any(e => e.Id == id);
        }
    }
} 