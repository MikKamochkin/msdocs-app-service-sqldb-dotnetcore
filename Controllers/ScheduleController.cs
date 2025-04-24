using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class ScheduleController : Controller
    {
        private readonly MyDatabaseContext _context;

        public ScheduleController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Schedule/Manage
        public async Task<IActionResult> Manage(Guid? teacherId, DateTime? date)
        {
            // 1) Teachers dropdown
            var teachers = await _context.Teacher
                .Select(t => new { t.Id, t.Name })
                .OrderBy(t => t.Name)
                .ToListAsync();
            ViewBag.Teachers = new SelectList(teachers, "Id", "Name", teacherId);

            // 2) Selected date (default = today)
            var selectedDate = (date ?? DateTime.Today).Date;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");

            // 3) Build 15‑minute time slots
            ViewBag.Times = Enumerable.Range(0, 24 * 4)
                .Select(i => TimeSpan.FromMinutes(i * 15))
                .Select(ts => new SelectListItem {
                    Value = ts.ToString(@"hh\:mm"),
                    Text  = ts.ToString(@"hh\:mm")
                })
                .ToList();

            // 4) Status dropdown
            ViewBag.Statuses = DropdownOptions.ScheduleStatusTypes;

            // 4.1) **Duration dropdown** (new)
            ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes;

            // 5) Groups (i.e. students) for that teacher
            List<SelectListItem> groupItems = new();
            if (teacherId.HasValue)
            {
                var assignments = await _context.Assignments
                    .Where(a => a.TeacherId == teacherId)
                    .Select(a => new {
                        a.Id,
                        a.GroupId,
                        GroupName = a.Group!.Name
                    })
                    .OrderBy(x => x.GroupName)
                    .ToListAsync();

                groupItems = assignments
                    .GroupBy(x => x.GroupId)
                    .Select(g => g.First())
                    .Select(a => new SelectListItem {
                        Value = a.Id.ToString(),
                        Text  = a.GroupName
                    })
                    .OrderBy(x => x.Text)
                    .ToList();
            }
            ViewBag.Groups = groupItems;

            // 6) Load existing schedule entries for that teacher + date
            var existing = new List<Schedule>();
            if (teacherId.HasValue)
            {
                existing = await _context.Schedule
                    .Include(s => s.Assignment!)
                        .ThenInclude(a => a.Group!)
                    .Where(s =>
                        s.Assignment!.TeacherId == teacherId &&
                        s.DateTime.Date             == selectedDate
                    )
                    .OrderBy(s => s.DateTime)
                    .ToListAsync();
            }

            // Flatten into JSON payload with group name & duration
            var flat = existing.Select(r => new {
                r.Id,
                r.AssignmentId,
                DateTime  = r.DateTime.ToString("o"),
                r.Status,
                r.Duration,
                GroupName = r.Assignment!.Group!.Name
            }).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(flat, new JsonSerializerOptions {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });

            return View(existing);
        }

        // POST: Schedule/Manage
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(
            Guid teacherId,
            DateTime selectedDate,
            List<Schedule> schedules,
            List<Guid>? toDelete)
        {
            // 1) remove any flagged-for-deletion rows
            if (toDelete != null)
            {
                foreach (var id in toDelete)
                {
                    var s = await _context.Schedule.FindAsync(id);
                    if (s != null)
                        _context.Schedule.Remove(s);
                }
            }

            foreach (var row in schedules)
            {
                if (row.DateTime.Kind == DateTimeKind.Utc)
                {
                    row.DateTime = row.DateTime.ToLocalTime();
                }
            }
            // 2) upsert the rest
            foreach (var row in schedules)
            {
                if (row.Id == Guid.Empty)
                {
                    row.Id = Guid.NewGuid();
                    _context.Schedule.Add(row);
                }
                else
                {
                    _context.Schedule.Update(row);
                }
            }

            await _context.SaveChangesAsync();

            // redirect back to the same teacher/date
            return RedirectToAction(nameof(Manage), new {
                teacherId,
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }
    }
}