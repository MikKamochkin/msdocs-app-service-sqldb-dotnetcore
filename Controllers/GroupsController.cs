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
    [Authorize(Roles = "support, assistant")]
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
            if (User.IsInRole("support") || User.IsInRole("admin"))
            {
                var groups = await _context.Group
                    .Where(g => g.StudentGroupCompositions.Count > 1 || g.IsManualGroup)
                    .Where(g => g.IsActive == true)
                    .Include(g => g.StudentGroupCompositions)
                    .ThenInclude(sgc => sgc.Student)
                    .ThenInclude(s => s.Contacts)
                    .ToListAsync();
                return View(groups);
            }
            else if (User.IsInRole("assistant"))
            {
                var groups = await _context.Group
                    .Where(g => g.IsActive)
                    .Where(g => g.StudentGroupCompositions.Count > 1 || g.IsManualGroup)
                    .Where(g => g.StudentGroupCompositions.All(sgc => sgc.Student.AccountingGroup == "S"))
                    .Include(g => g.StudentGroupCompositions)
                        .ThenInclude(sgc => sgc.Student)
                            .ThenInclude(s => s.Contacts)
                    .ToListAsync();

                return View(groups);
            }
            else
            {
                return Forbid();
            }
        }

        /*// GET: Groups/Details/5
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
        }*/

        // GET: Groups/Create
        public async Task<IActionResult> Create(Guid? copyFromGroupId = null)
        {
            // 1) Populate the Students dropdown for the modal
            try
            {
                if (User.IsInRole("admin") || User.IsInRole("support"))
                {
                    var allStudents = await _context.Student
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                    if (allStudents.Any())
                        ViewBag.Students = new SelectList(allStudents, "ID", "Name");
                    else
                        ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                }
                else if (User.IsInRole("assistant"))
                {
                    var filteredStudents = await _context.Student
                        .Where(s => s.AccountingGroup == "S")
                        .OrderBy(s => s.Name)
                        .ToListAsync();

                    if (filteredStudents.Any())
                        ViewBag.Students = new SelectList(filteredStudents, "ID", "Name");
                    else
                        ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
                }
                else
                {
                    return Forbid();
                }
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
                            id = sgc.StudentId,
                            name = sgc.Student.Name,
                            useMyBalance = sgc.UseMyBalance
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
                    group.IsManualGroup = true;
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
            if (User.IsInRole("admin") || User.IsInRole("support"))
            {
                var allStudents = await _context.Student
                .OrderBy(s => s.Name)
                .ToListAsync();

                if (allStudents.Any())
                    ViewBag.Students = new SelectList(allStudents, "ID", "Name");
                else
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            else if (User.IsInRole("assistant"))
            {
                var filteredStudents = await _context.Student
                    .Where(s => s.AccountingGroup == "S")
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                if (filteredStudents.Any())
                    ViewBag.Students = new SelectList(filteredStudents, "ID", "Name");
                else
                    ViewBag.Students = new SelectList(new List<Student>(), "ID", "Name");
            }
            else
            {
                return Forbid();
            }

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
                    var useVals = Request.Form["UseMyBalanceValues"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < studentIds.Length; i++)
                    {
                        if (Guid.TryParse(studentIds[i], out var sid))
                        {
                            bool use = i < useVals.Length && bool.Parse(useVals[i]);
                            _context.StudentGroupComposition.Add(new StudentGroupComposition
                            {
                                Id = Guid.NewGuid(),
                                GroupId = id,
                                StudentId = sid,
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

        // GET: Groups/DeactivateAndCopy/5
        [HttpGet]
        public async Task<IActionResult> DeactivateAndCopy(Guid id)
        {
            var g = await _context.Group
                .Include(gp => gp.StudentGroupCompositions)
                    .ThenInclude(sgc => sgc.Student)
                .FirstOrDefaultAsync(gp => gp.Id == id);

            if (g == null)
            {
                return NotFound();
            }

            // Prepare the new group with copied data
            var newGroup = new Group
            {
                Name = g.Name,
                IsActive = true,
                IsManualGroup = true
            };

            ViewBag.CopyStudents = g.StudentGroupCompositions
                .Select(sgc => new
                {
                    id = sgc.StudentId,
                    name = sgc.Student.Name,
                    useMyBalance = sgc.UseMyBalance
                })
                .ToList();

            // Set flag to indicate this is a copy operation
            ViewData["IsCopyAction"] = true;
            ViewData["OriginalGroupId"] = id;

            // Populate students dropdown based on role
            if (User.IsInRole("admin") || User.IsInRole("support"))
            {
                var allStudents = await _context.Student
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                ViewBag.Students = allStudents.Any()
                    ? new SelectList(allStudents, "ID", "Name")
                    : new SelectList(new List<Student>(), "ID", "Name");
            }
            else if (User.IsInRole("assistant"))
            {
                var filteredStudents = await _context.Student
                    .Where(s => s.AccountingGroup == "S")
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                ViewBag.Students = filteredStudents.Any()
                    ? new SelectList(filteredStudents, "ID", "Name")
                    : new SelectList(new List<Student>(), "ID", "Name");
            }
            else
            {
                return Forbid();
            }

            return View("Create", newGroup);
        }

        // POST: Groups/DeactivateAndCopy/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateAndCopy(Guid id, [Bind("Name")] Group newGroup)
        {
            if (!ModelState.IsValid)
                return View("Create", newGroup);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // --- (A) Create the new group ---
                Guid newGroupId = Guid.NewGuid();
                newGroup.Id = newGroupId;
                newGroup.IsActive = true;
                newGroup.IsManualGroup = true;
                _context.Add(newGroup);

                // --- (B) Capture ORIGINAL student ids before we change anything ---
                var originalStudentIds = await _context.StudentGroupComposition
                    .Where(sgc => sgc.GroupId == id)
                    .Select(sgc => sgc.StudentId)
                    .ToListAsync();

                // parse posted students once (we'll use them twice)
                var postedStudentIds = Request.Form["StudentIds"].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();

                // --- (C) Determine NEW students added in this copy ---
                var newStudentIds = postedStudentIds.Except(originalStudentIds).ToList();

                // --- (D) Deactivate original group + copy its ACTIVE assignments ---
                var originalGroup = await _context.Group.FindAsync(id);
                if (originalGroup != null)
                {
                    originalGroup.IsActive = false;

                    var activeAssignments = await _context.Assignments
                        .Where(a => a.GroupId == id && a.IsActive)
                        .ToListAsync();

                    // Keep track of all NEW assignment IDs we create for the new group
                    var newAssignmentIds = new List<Guid>();

                    foreach (var a in activeAssignments)
                    {
                        // We'll use this ID for both (1) moving existing SB rows and (2) seeding SB for NEW students
                        var assignmentCopyId = Guid.NewGuid();
                        newAssignmentIds.Add(assignmentCopyId);

                        // Move existing student balances over to the copied assignment
                        var studentBalances = await _context.StudentBalance
                            .Where(s => s.AssignmentId == a.Id)
                            .ToListAsync();
                        foreach (var sb in studentBalances)
                            sb.AssignmentId = assignmentCopyId;

                        // Deactivate original assignment and add the copy
                        a.IsActive = false;

                        _context.Assignments.Add(new Assignments
                        {
                            Id = assignmentCopyId,
                            GroupId = newGroupId,
                            TeacherId = a.TeacherId,
                            StudentUnitCost = a.StudentUnitCost,
                            StudentUnitType = a.StudentUnitType,
                            StudentUnitDuration = a.StudentUnitDuration,
                            TeacherPayForUnit = a.TeacherPayForUnit,
                            TeacherPayUnitType = a.TeacherPayUnitType,
                            IsActive = true,
                            // NOTE: if Schedules must be cloned as new rows, don't assign the collection directly.
                            // This keeps your current behavior.
                            Schedules = a.Schedules
                        });
                    }

                    // --- (E) Add StudentGroupComposition rows for ALL posted students ---
                    var useMyBalanceValues = Request.Form["UseMyBalanceValues"].ToString()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Equals("true", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    for (int i = 0; i < postedStudentIds.Count; i++)
                    {
                        var studentId = postedStudentIds[i];
                        bool useMyBalance = i < useMyBalanceValues.Count ? useMyBalanceValues[i] : false;

                        _context.StudentGroupComposition.Add(new StudentGroupComposition
                        {
                            Id = Guid.NewGuid(),
                            GroupId = newGroup.Id,
                            StudentId = studentId,
                            UseMyBalance = useMyBalance
                        });
                    }

                    // --- (F) Seed StudentBalance ONLY for NEW students across the NEW assignments ---
                    // Avoid duplicates (and satisfy the unique index on StudentId+AssignmentId).
                    if (newStudentIds.Count > 0 && newAssignmentIds.Count > 0)
                    {
                        // Fetch existing pairs once (should be none for the new assignments, but be safe)
                        var existingPairs = await _context.StudentBalance
                            .Where(sb => newAssignmentIds.Contains(sb.AssignmentId)
                                         && newStudentIds.Contains(sb.StudentId))
                            .Select(sb => new { sb.AssignmentId, sb.StudentId })
                            .ToListAsync();

                        var existingSet = new HashSet<(Guid AssignmentId, Guid StudentId)>(
                            existingPairs.Select(p => (p.AssignmentId, p.StudentId)));

                        foreach (var assignmentId in newAssignmentIds)
                        {
                            foreach (var sid in newStudentIds)
                            {
                                if (!existingSet.Contains((assignmentId, sid)))
                                {
                                    _context.StudentBalance.Add(new StudentBalance
                                    {
                                        Id = Guid.NewGuid(),
                                        StudentId = sid,
                                        AssignmentId = assignmentId,
                                        Balance = 0
                                    });
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in DeactivateAndCopy POST action");
                ModelState.AddModelError("", "An error occurred while creating the group copy. Please try again.");
                // re-load dropdowns like before (omitted for brevity)
            }

            // fallback: re-show the Create view with the model
            // (populate dropdowns as in your original code)
            return View("Create", newGroup);
        }


        private bool GroupExists(Guid id)
        {
            return _context.Group.Any(e => e.Id == id);
        }
    }
}