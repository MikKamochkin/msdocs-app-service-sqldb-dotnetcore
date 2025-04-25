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
using System.Net;
using System.Runtime.InteropServices;
using System.Data;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
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
                .Where(g => g.StudentGroupCompositions.Count > 1)
                .Where (g => g.IsActive == true)
                .Include(g => g.StudentGroupCompositions)
                .ThenInclude(sgc => sgc.Student)
                .ThenInclude(s => s.Contacts)
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
        public async Task<IActionResult> Create(Guid? copyFromGroupId = null)
        {
            // 1) Populate the Students dropdown for the modal
            try
            {
                var allStudents = await _context.Student
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                if (allStudents.Any())
                    ViewBag.Students = new SelectList(allStudents, "ID", "Name");
                else
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load students for Create view");
                ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }

            // 2) Prepare a fresh Group model
            var model = new Group
            {
                Name = ""     // user will fill or we’ll override below if copying
                // Id and IsActive left to defaults
            };

            // 3) If we’re copying from an existing group, fetch its data
            if (copyFromGroupId.HasValue)
            {
                var sourceGroup = await _context.Group
                    .Include(g => g.StudentGroupCompositions)
                        .ThenInclude(sgc => sgc.Student)
                    .FirstOrDefaultAsync(g => g.Id == copyFromGroupId.Value);

                if (sourceGroup != null)
                {
                    // Copy the name
                    model.Name = sourceGroup.Name;

                    // Build a simple list of { id, name, useMyBalance } for the client script
                    ViewBag.CopyStudents = sourceGroup.StudentGroupCompositions
                        .Select(sgc => new
                        {
                            id            = sgc.StudentId,
                            name          = sgc.Student.Name,
                            useMyBalance  = sgc.UseMyBalance
                        })
                        .ToList();
                }
            }

            return View(model);
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
                    // 1) Create the group
                    group.Id = Guid.NewGuid();
                    group.IsActive = true;
                    _context.Add(group);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Group created with ID: {group.Id}");

                    // 2) Insert a “blank” Assignments record (only GroupId, all other non-nullable defaults)
                    /*var blankAssignment = new Assignments
                    {
                        Id                 = Guid.NewGuid(),
                        GroupId            = group.Id,
                        //TeacherId          = Guid.Empty,      // no teacher assigned yet
                        StudentUnitCost    = 0f,              // default float
                        StudentUnitType    = string.Empty,    // required string
                        StudentUnitBalance = 0f,
                        StudentUnitDuration= 0f,
                        TeacherPayForUnit  = 0f,
                        TeacherPayUnitType = string.Empty,
                        IsActive           = false
                    };
                    _context.Assignments.Add(blankAssignment);
                    await _context.SaveChangesAsync();*/

                    _logger.LogInformation($"Blank assignment created for group ID: {group.Id}");

                    // 3) Handle the StudentGroupComposition entries
                    var studentIds = Request.Form["StudentIds"].ToString().Split(',');
                    var useMyBalanceValues = Request.Form["UseMyBalanceValues"].ToString().Split(',');

                    _logger.LogInformation($"Found {studentIds.Length} student IDs");

                    for (int i = 0; i < studentIds.Length; i++)
                    {
                        if (Guid.TryParse(studentIds[i], out Guid studentId))
                        {
                            bool useMyBalance = i < useMyBalanceValues.Length
                                && useMyBalanceValues[i].ToLower() == "true";

                            _logger.LogInformation($"Processing student {studentId} with UseMyBalance: {useMyBalance}");

                            var composition = new StudentGroupComposition
                            {
                                Id            = Guid.NewGuid(),
                                GroupId       = group.Id,
                                StudentId     = studentId,
                                UseMyBalance  = useMyBalance
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

            // If we get here, something failed; reload students for the dropdown and show the form again
            try
            {
                var students = _context.Student.OrderBy(s => s.Name).ToList();
                if (students.Any())
                    ViewBag.Students = new SelectList(students, "ID", "Name");
                else
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
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
            if (id == null) return NotFound();

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .ThenInclude(sgc => sgc.Student)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            // populate dropdown
            var students = await _context.Student
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewBag.Students = new SelectList(students, "ID", "Name");

            return View(group);
        }

        // POST: Groups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] Group group)
        {
            if (id != group.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // update the name only
                    _context.Entry(group).Property(g => g.Name).IsModified = true;

                    // clear out old compositions
                    var old = _context.StudentGroupComposition
                                    .Where(sgc => sgc.GroupId == id);
                    _context.StudentGroupComposition.RemoveRange(old);

                    // re-add from the hidden fields
                    var studentIds = Request.Form["StudentIds"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var useVals   = Request.Form["UseMyBalanceValues"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < studentIds.Length; i++)
                    {
                        if (Guid.TryParse(studentIds[i], out var sid))
                        {
                            bool use = i < useVals.Length && bool.Parse(useVals[i]);
                            _context.StudentGroupComposition.Add(new StudentGroupComposition {
                                Id           = Guid.NewGuid(),
                                GroupId      = id,
                                StudentId    = sid,
                                UseMyBalance = use
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GroupExists(group.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // on error, repopulate dropdown and return view
            var studentsList = await _context.Student.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Students = new SelectList(studentsList, "ID", "Name");
            return View(group);
        }
        /*

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
        */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var g = await _context.Group.FindAsync(id);
            if (g != null)
            {
                g.IsActive = false;

                // grab only the assignments that are still active
                var assignments = await _context.Assignments
                    .Where(a => a.GroupId == id && a.IsActive)
                    .ToListAsync();

                _logger.LogInformation($"Deactivate: found {assignments.Count} active assignments for Group {id}");

                foreach (var a in assignments)
                    a.IsActive = false;

                // since these were loaded and tracked, changing the property is enough
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateAndCopy(Guid id)
        {
            var g = await _context.Group
                .Include(gp => gp.StudentGroupCompositions)
                    .ThenInclude(sgc => sgc.Student)
                .FirstOrDefaultAsync(gp => gp.Id == id);

            if (g != null)
            {
                g.IsActive = false;

                var assignments = await _context.Assignments
                    .Where(a => a.GroupId == id && a.IsActive)
                    .ToListAsync();

                _logger.LogInformation($"DeactivateAndCopy: found {assignments.Count} active assignments for Group {id}");

                foreach (var a in assignments)
                    a.IsActive = false;

                await _context.SaveChangesAsync();

                // now set up the copy
                var newGroup = new Group
                {
                    Name     = g.Name,
                    IsActive = true
                };

                ViewBag.CopyStudents = g.StudentGroupCompositions
                    .Select(sgc => new {
                        id            = sgc.StudentId,
                        name          = sgc.Student.Name,
                        useMyBalance  = sgc.UseMyBalance
                    })
                    .ToList();

                var students = await _context.Student.OrderBy(s => s.Name).ToListAsync();
                ViewBag.Students = new SelectList(students, "ID", "Name");

                return View("Create", newGroup);
            }

            return RedirectToAction(nameof(Index));
        }



        private bool GroupExists(Guid id)
        {
            return _context.Group.Any(e => e.Id == id);
        }
    }
} 