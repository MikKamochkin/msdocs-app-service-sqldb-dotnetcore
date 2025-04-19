using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "admin")]
    public class ScheduleController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(
            MyDatabaseContext context,
            ILogger<ScheduleController> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // GET: Schedule
        public async Task<IActionResult> Index()
        {
            var schedules = await _context.Schedule
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Group)
                .OrderBy(s => s.DateTime)
                .ToListAsync();
            return View(schedules);
        }

        // GET: Schedule/Create
        public IActionResult Create()
        {
            PopulateAssignmentsDropDown();
            ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes;
            ViewBag.ScheduleStatusTypes = DropdownOptions.ScheduleStatusTypes;
            var model = new Schedule {
                DateTime = DateTime.Now,
                Status   = string.Empty,
                Duration = 0
            };
            return View(model);
        }

        // POST: Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("AssignmentId,DateTime,Status,Duration")] Schedule schedule)
        {
            _logger.LogInformation("Schedule/Create POST called; valid? {Valid}", ModelState.IsValid);

            if (!ModelState.IsValid)
            {
                // Log out validation errors
                var errors = ModelState
                    .SelectMany(kvp => kvp.Value?.Errors
                        .Select(err => $"{kvp.Key}: {err.ErrorMessage}")
                    ?? Enumerable.Empty<string>())
                    .ToList();
                _logger.LogWarning("Create validation failed: {Errors}", string.Join("; ", errors));

                PopulateAssignmentsDropDown(schedule.AssignmentId);
                return View(schedule);
            }

            schedule.Id = Guid.NewGuid();
            _context.Add(schedule);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Schedule/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var schedule = await _context.Schedule.FindAsync(id);
            if (schedule == null) return NotFound();
            ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes;
            ViewBag.ScheduleStatusTypes = DropdownOptions.ScheduleStatusTypes;
            PopulateAssignmentsDropDown(schedule.AssignmentId);
            return View(schedule);
        }

        // POST: Schedule/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("Id,AssignmentId,DateTime,Status,Duration")] Schedule schedule)
        {
            if (id != schedule.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                PopulateAssignmentsDropDown(schedule.AssignmentId);
                return View(schedule);
            }

            try
            {
                _context.Update(schedule);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Schedule.Any(e => e.Id == schedule.Id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        private void PopulateAssignmentsDropDown(Guid? selectedId = null)
        {
            var list = _context.Assignments
                .Include(a => a.Group)
                .Select(a => new { a.Id, Display = a.Group.Name })
                .OrderBy(x => x.Display)
                .ToList();
            ViewBag.Assignments = new SelectList(list, "Id", "Display", selectedId);
        }
    }
}
