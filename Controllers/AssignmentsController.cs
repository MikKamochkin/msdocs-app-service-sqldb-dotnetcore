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

        // POST: Assignments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Assignments assignment)
        {
            assignment.IsActive = true;
            
            if (!ModelState.IsValid)
            {
                // Log or inspect validation failures
                var errors = ModelState.Values
                                .SelectMany(v => v.Errors)
                                .Select(e => e.ErrorMessage);
                _logger.LogWarning("Create Assignment invalid: {Errors}", string.Join(" | ", errors));

                await PopulateDropdowns(assignment);
                return View(assignment);
            }

            assignment.Id = Guid.NewGuid();
            _context.Assignments.Add(assignment);

            //Create a studentBalance entry for this assignment for all students in the group

            var group = await _context.Group
                .Include(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(g => g.Id == assignment.GroupId);

            if (group != null)
            {
                foreach (var sgc in group.StudentGroupCompositions)
                {
                    Guid studentId = sgc.StudentId;
                    Guid studentBalanceId = Guid.NewGuid();
                    _context.StudentBalance.Add(new StudentBalance
                    {
                        Id = studentBalanceId,
                        StudentId = studentId,
                        AssignmentId = assignment.Id,
                        Balance = 0
                    });

                    //_logger.LogInformation("Created a studentBalance entry with Id: {a} for AssignmentId: {a} for StudentId: {b}", studentBalanceId, assignment.Id, studentId);
                }
            }

            //Creating new conversation for teacher and student only if it's a private class

            if (group != null)
            {
                bool privateLesson = group.StudentGroupCompositions.Count == 1;

                if (privateLesson)
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
            }

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
