using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        // GET: Schedule/Manage
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Manage(Guid? teacherId, DateTime? date)
        {
            // 1) Teachers dropdown
            var teachers = await _context.Teacher
                .Select(t => new { t.Id, t.Name })
                .OrderBy(t => t.Name)
                .ToListAsync();
            ViewBag.Teachers = new SelectList(teachers, "Id", "Name", teacherId);

            // 2) Selected date (default today)
            var selectedDate = (date ?? DateTime.Today).Date;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");

            // 3) 15‑min time slots
            ViewBag.Times = Enumerable
                .Range(0, 24 * 4)
                .Select(i => TimeSpan.FromMinutes(i * 15))
                .Select(ts => new SelectListItem {
                    Value = ts.ToString(@"hh\:mm"),
                    Text  = ts.ToString(@"hh\:mm")
                })
                .ToList();

            // 4) Status dropdown
            ViewBag.Statuses = DropdownOptions.ScheduleStatusTypes;

            // 5) Student‑groups for this teacher
            var groupItems = new List<SelectListItem>();
            if (teacherId.HasValue)
            {
                groupItems = await _context.Assignments
                    .Include(a => a.Group)
                    .Where(a => a.TeacherId == teacherId)
                    .Select(a => new SelectListItem {
                        Value = a.Id.ToString(),
                        Text  = a.Group.Name
                    })
                    .OrderBy(x => x.Text)
                    .ToListAsync();
            }
            ViewBag.Groups = groupItems;

            // 6) Load existing schedules for teacher + date
            var existing = new List<Schedule>();
            if (teacherId.HasValue)
            {
                existing = await _context.Schedule
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Group)
                    .Where(s =>
                        s.Assignment.TeacherId == teacherId
                        && s.DateTime.Date        == selectedDate
                    )
                    .ToListAsync();
            }

            // 7) Flatten into cycle‑free JSON, including the group’s name
            var flat = existing
                .Select(r => new {
                    r.Id,
                    r.AssignmentId,
                    DateTime  = r.DateTime.ToString("o"),
                    r.Status,
                    r.Duration,
                    GroupName = r.Assignment.Group.Name
                })
                .ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flat,
                new JsonSerializerOptions {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                }
            );

            return View(existing);
        }

        // POST: Schedule/Manage
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Manage(
            Guid teacherId,
            DateTime selectedDate,
            List<Schedule> schedules)
        {
            // 1) Upsert each posted row
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

            // 2) Redirect back to the same teacher+date
            return RedirectToAction(nameof(Manage), new {
                teacherId,
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }
    }
}
