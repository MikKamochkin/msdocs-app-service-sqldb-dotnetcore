// Controllers/AssignmentsController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Authorization;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin, assistant") ]
    public class AssignmentsController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<AssignmentsController> _logger;

        public AssignmentsController( MyDatabaseContext context, ILogger<AssignmentsController> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // GET: Assignments
        public async Task<IActionResult> Index()
        {
            var assignments = await _context.Assignments
                .Include(a => a.Group)
                .Include(a => a.Teacher)
                .Where(a => a.IsActive == true)
                .ToListAsync();
            return View(assignments);
        }

        // GET: Assignments/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var a = await _context.Assignments
                .Include(x => x.Group)
                .Include(x => x.Teacher)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            return View(a);
        }
        
        // GET: Assignments/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var assignment = new Assignments
            {
                StudentUnitCost     = 0f,
                StudentUnitType     = "CAD",
                StudentUnitDuration = 60f,
                TeacherPayForUnit   = 0f,
                TeacherPayUnitType  = "CAD",
                IsActive            = true
            };
            await PopulateDropdowns(assignment);
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Assignments assignment,
            string? existingAssignmentChoice = null,
            Guid? reviewedGroupId = null,
            Guid[]? reviewedAssignmentIds = null)
        {
            assignment.IsActive = true;

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);

                _logger.LogWarning(
                    "Create Assignment invalid: {Errors}",
                    string.Join(" | ", errors));

                await PopulateDropdowns(assignment);
                return View(assignment);
            }

            var duplicate = await _context.Assignments.FirstOrDefaultAsync(a =>
                    a.GroupId == assignment.GroupId &&
                    a.TeacherId == assignment.TeacherId &&
                    //a.StudentUnitCost == assignment.StudentUnitCost &&
                    //a.StudentUnitType == assignment.StudentUnitType &&
                    a.StudentUnitDuration == assignment.StudentUnitDuration &&
                    //a.TeacherPayForUnit == assignment.TeacherPayForUnit &&
                    //a.TeacherPayUnitType == assignment.TeacherPayUnitType &&
                    a.IsActive == assignment.IsActive);

            if (duplicate != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "An assignment with the same student, teacher, and duration already exists. " +
                    "No duplicate was created. Please edit the existing assignment instead.");

                if (duplicate != null)
                {
                    ViewBag.DuplicateAssignmentId = duplicate.Id;
                }
                

                await PopulateDropdowns(assignment);
                return View(assignment);
            }

            bool deactivatedAssignmentExists = await _context.Assignments.AnyAsync(a =>
                a.GroupId == assignment.GroupId &&
                a.TeacherId == assignment.TeacherId &&
                a.StudentUnitCost == assignment.StudentUnitCost &&
                a.StudentUnitType == assignment.StudentUnitType &&
                a.StudentUnitDuration == assignment.StudentUnitDuration &&
                a.TeacherPayForUnit == assignment.TeacherPayForUnit &&
                a.TeacherPayUnitType == assignment.TeacherPayUnitType &&
                a.IsActive == assignment.IsActive == false);

            if (deactivatedAssignmentExists)
            {
                var deactivatedAssignment = await _context.Assignments.FirstOrDefaultAsync(a =>
                    a.GroupId == assignment.GroupId &&
                    a.TeacherId == assignment.TeacherId &&
                    a.StudentUnitCost == assignment.StudentUnitCost &&
                    a.StudentUnitType == assignment.StudentUnitType &&
                    a.StudentUnitDuration == assignment.StudentUnitDuration &&
                    a.TeacherPayForUnit == assignment.TeacherPayForUnit &&
                    a.TeacherPayUnitType == assignment.TeacherPayUnitType &&
                    a.IsActive == false);
                
                if (deactivatedAssignment != null)
                {
                    deactivatedAssignment.IsActive = true;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(g => g.Id == assignment.GroupId);

            if (group == null)
            {
                ModelState.AddModelError(
                    nameof(Assignments.GroupId),
                    "Please select a valid group.");

                await PopulateDropdowns(assignment);
                return View(assignment);
            }

            var studentIds = group.StudentGroupCompositions
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToList();

            // Find active assignments sharing any student with the selected group.
            var existingAssignments = await _context.Assignments
                .Include(a => a.Group)
                    .ThenInclude(g => g.StudentGroupCompositions)
                .Include(a => a.Teacher)
                .Where(a => a.IsActive &&
                    a.Group.StudentGroupCompositions
                        .Any(sgc => studentIds.Contains(sgc.StudentId)))
                // Keep memberships in the group receiving the new assignment.
                // Existing private assignments for that same student can still be replaced.
                .Where(a => a.GroupId != assignment.GroupId ||
                    (!a.Group.IsManualGroup &&
                    a.Group.StudentGroupCompositions.Count == 1))
                .OrderBy(a => a.Group.Name)
                .ThenBy(a => a.Id)
                .ToListAsync();

            var currentIds = existingAssignments
                .Select(a => a.Id)
                .ToHashSet();

            bool validChoice =
                existingAssignmentChoice == "deactivate" ||
                existingAssignmentChoice == "keep";

            bool reviewedCurrentAssignments =
                reviewedGroupId == assignment.GroupId &&
                currentIds.SetEquals(reviewedAssignmentIds ?? Array.Empty<Guid>());

            if (existingAssignments.Count > 0 &&
                (!validChoice || !reviewedCurrentAssignments))
            {
                ViewBag.ExistingAssignments = existingAssignments;

                await PopulateDropdowns(assignment);
                return View(assignment);
            }

            if (existingAssignmentChoice == "deactivate" &&
                reviewedCurrentAssignments)
            {
                // Process each group once, even if it has multiple active assignments.
                foreach (var assignmentsByGroup in existingAssignments.GroupBy(a => a.GroupId))
                {
                    var oldGroup = assignmentsByGroup.First().Group;

                    bool isGroupClass = oldGroup.IsManualGroup ||
                        oldGroup.StudentGroupCompositions.Count > 1;

                    //!!!!!!!!!!!!!!!!
                    //also remove/deactivate their student balance records for the old assignments
                    if (isGroupClass)
                    {
                        // Never remove students from the group receiving the new assignment.
                        if (oldGroup.Id == assignment.GroupId)
                            continue;

                        // Remove only the matching students; keep the group and assignments active.
                        var membershipsToRemove = oldGroup.StudentGroupCompositions
                            .Where(sgc => studentIds.Contains(sgc.StudentId))
                            .ToList();

                        _context.StudentGroupComposition.RemoveRange(membershipsToRemove);
                    }
                    else
                    {
                        foreach (var oldAssignment in assignmentsByGroup)
                        {
                            oldAssignment.IsActive = false;
                        }
                    }
                }
            }

            assignment.Id = Guid.NewGuid();
            _context.Assignments.Add(assignment);

            foreach (var studentId in studentIds)
            {
                _context.StudentBalance.Add(new StudentBalance
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    AssignmentId = assignment.Id,
                    Balance = 0
                });
            }

            if (group.StudentGroupCompositions.Count == 1)
            {
                _context.Conversations.Add(new Conversations
                {
                    Id = Guid.NewGuid(),
                    TeacherId = assignment.TeacherId,
                    StudentID = group.StudentGroupCompositions.First().StudentId,
                    IsActive = true,
                    LastMessageTime = null
                });
            }

            // Save the new assignment and any deactivations together.
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }




        // GET: Assignments/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var a = await _context.Assignments
                .Include(x => x.Group)
                .Include(x => x.Teacher)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            await PopulateDropdowns(a);
            return View(a);
        }

        // POST: Assignments/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id)
        {
            // 1) Load the tracked entity
            var assignment = await _context.Assignments
                .Include(a => a.Group)
                .Include(a => a.Teacher)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            // 2) Try to update only these properties from the form values
            if (await TryUpdateModelAsync(
                    assignment,
                    prefix: "",    // no prefix since your form fields are named e.g. StudentUnitType, etc.
                    a => a.GroupId,
                    a => a.TeacherId,
                    a => a.StudentUnitCost,
                    a => a.StudentUnitType,
                    a => a.StudentUnitDuration,
                    a => a.TeacherPayForUnit,
                    a => a.TeacherPayUnitType,
                    a => a.IsActive))
            {
                // 3) All binding & validation succeeded → save & redirect
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // 4) Something failed (ModelState invalid) → re-populate dropdowns and re-show form
            await PopulateDropdowns(assignment);
            return View(assignment);
        }


        // Shared helper to populate all dropdown lists
        private async Task PopulateDropdowns(Assignments model)
        {
            ViewBag.Groups        = new SelectList(
                                       await _context.Group
                                       .Where(g => g.IsActive)
                                       .OrderBy(g => g.Name)
                                       .ToListAsync(),
                                       "Id", "Name",
                                       model.GroupId);
            ViewBag.Teachers      = new SelectList(
                                       await _context.Teacher
                                       .OrderBy(t => t.Name)
                                       .ToListAsync(),
                                       "Id", "Name",
                                       model.TeacherId);

            ViewBag.PayUnits      = DropdownOptions.PayUnitTypes;
            ViewBag.StatusTypes   = DropdownOptions.ScheduleStatusTypes;
            ViewBag.DurationTypes = DropdownOptions.LessonDurationTypes;
        }
    }
}
