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
using TimeZoneConverter;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin")]
    public class ScheduleController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IHubContext<ScheduleHub> _hub;
        private readonly ILogger<TeachersController> _logger;

        public ScheduleController(MyDatabaseContext context, IHubContext<ScheduleHub> hub, ILogger<TeachersController> logger)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
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
            ViewBag.Times = Enumerable.Range(0, 24 * 12)
                .Select(i => TimeSpan.FromMinutes(i * 5))
                .Select(ts => new SelectListItem
                {
                    Value = ts.ToString(@"hh\:mm"),
                    Text = ts.ToString(@"hh\:mm"),
                    Selected = ts == TimeSpan.FromHours(9)
                })
                .ToList();

            // 4) Status dropdown
            ViewBag.Statuses = DropdownOptions.ScheduleStatusTypes
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "Scheduled"
                });

            // 4.1) Duration dropdown (new)
            //ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes;
            ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "60"
                })
                .ToList();

            // 5) Time zones
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == defaultZoneIana;
            }
            ViewBag.TimeZones = timeZones;

            // 6) Groups (i.e. students) for that teacher
            List<SelectListItem> groupItems = new();
            if (teacherId.HasValue)
            {
                var assignments = await _context.Assignments
                    .Where(a => a.TeacherId == teacherId)
                    .Select(a => new
                    {
                        a.Id,
                        a.GroupId,
                        GroupName = a.Group!.Name
                    })
                    .OrderBy(x => x.GroupName)
                    .ToListAsync();

                groupItems = assignments
                    .GroupBy(x => x.GroupId)
                    .Select(g => g.First())
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = a.GroupName
                    })
                    .OrderBy(x => x.Text)
                    .ToList();
            }
            ViewBag.Groups = groupItems;

            // 7) Load existing schedule entries for that teacher + date via UTC range
            var existing = new List<Schedule>();
            if (teacherId.HasValue)
            {
                // Convert selected date (local) bounds into UTC
                var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

                var localStart = selectedDate;          // 00:00 local
                var localEnd = selectedDate.AddDays(1); // next midnight

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tzInfo);

                existing = await _context.Schedule
                    .Include(s => s.Assignment!)
                        .ThenInclude(a => a.Group!)
                    .Where(s =>
                        s.Assignment!.TeacherId == teacherId &&
                        s.DateTime >= startUtc &&
                        s.DateTime < endUtc
                    )
                    .OrderBy(s => s.DateTime)
                    .ToListAsync();
            }

            // Flatten into JSON payload with UTC ISO strings
            var flat = existing.Select(r => new
            {
                r.Id,
                r.AssignmentId,
                DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                r.Status,
                r.Duration,
                GroupName = r.Assignment!.Group!.Name
            }).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(flat, new JsonSerializerOptions
            {
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
            List<Guid>? toDelete,
            int timeZoneOffset)
        {
            // 1) Remove any flagged-for-deletion rows
            if (toDelete != null)
            {
                foreach (var id in toDelete)
                {
                    var s = await _context.Schedule.FindAsync(id);
                    if (s != null)
                        _context.Schedule.Remove(s);
                }
            }

            // 2) Convert any local DateTime to UTC before saving
            foreach (var row in schedules)
            {
                if (row.DateTime.Kind == DateTimeKind.Local)
                {
                    row.DateTime = row.DateTime.ToUniversalTime();
                }
            }

            // 3) Upsert the rest
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

            var assignmentIds = schedules.Select(s => s.AssignmentId).Distinct().ToList();

            // Query StudentGroupCompositions linked to those assignments via GroupId
            var affectedStudentIds = await _context.Assignments
                .Where(a => assignmentIds.Contains(a.Id))
                .Include(a => a.Group)
                    .ThenInclude(g => g.StudentGroupCompositions)
                .SelectMany(a => a.Group!.StudentGroupCompositions)
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToListAsync();

            foreach (var studentId in affectedStudentIds)
            {
                _logger.LogInformation("Edited student with studentId: " + studentId);
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }



            //
            // ─── Build “updatedSchedule” JSON for that teacher/date ───
            //
            // 3.1) Convert `selectedDate` (which is local) → UTC range using your server’s default zone
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            var localStart = selectedDate.Date;            // midnight local
            var localEnd = localStart.AddDays(1);        // next midnight
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tzInfo);

            // 3.2) Instead of “today’s” window, always grab the most recent 15 lessons for this teacher:
            var updatedSchedule = await _context.Schedule
                .Include(s => s.Assignment!).ThenInclude(a => a.Teacher)
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .Select(r => new
                {
                    r.Id,
                    r.AssignmentId,
                    DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                    r.Status,
                    r.Duration,
                    TeacherId = r.Assignment!.Teacher!.Id,
                    TeacherName = r.Assignment!.Teacher!.Name
                })
                .ToListAsync();

            _logger.LogInformation("Broadcasting ScheduleChanged to teacher_{TeacherId}, count={Count}", teacherId, updatedSchedule.Count);
            _logger.LogDebug("Payload: {PayloadJson}", JsonSerializer.Serialize(updatedSchedule));


            // 3.3) Broadcast to the “teacher_{teacherId}” group:
            await _hub.Clients
                .Group($"teacher_{teacherId}")
                .SendAsync("ScheduleChanged", updatedSchedule);

            // 4) Redirect as before
            return RedirectToAction(nameof(Manage), new
            {
                teacherId,
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyLastWeek(Guid teacherId, DateTime selectedDate)
        {
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            var lastWeekStartLocal = selectedDate.Date.AddDays(-7);
            var lastWeekEndLocal = lastWeekStartLocal.AddDays(1);

            var lastWeekStartUtc = TimeZoneInfo.ConvertTimeToUtc(lastWeekStartLocal, tzInfo);
            var lastWeekEndUtc = TimeZoneInfo.ConvertTimeToUtc(lastWeekEndLocal, tzInfo);

            var lastWeekEntries = await _context.Schedule
                .Include(s => s.Assignment!)
                .Where(s =>
                    s.Assignment!.TeacherId == teacherId &&
                    s.DateTime >= lastWeekStartUtc &&
                    s.DateTime < lastWeekEndUtc)
                .ToListAsync();

            var newEntries = new List<Schedule>();

            foreach (var entry in lastWeekEntries)
            {
                var newDate = entry.DateTime.AddDays(7);

                var exists = await _context.Schedule.AnyAsync(s =>
                    s.Assignment!.TeacherId == teacherId &&
                    s.DateTime == newDate);

                if (exists)
                    continue;

                var copy = new Schedule
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = entry.AssignmentId,
                    DateTime = newDate,
                    Status = entry.Status,
                    Duration = entry.Duration
                };

                newEntries.Add(copy);
                _context.Schedule.Add(copy);
            }

            if (newEntries.Count > 0)
            {
                await _context.SaveChangesAsync();

                var assignmentIds = newEntries.Select(e => e.AssignmentId).Distinct().ToList();

                var affectedStudentIds = await _context.Assignments
                    .Where(a => assignmentIds.Contains(a.Id))
                    .Include(a => a.Group)
                        .ThenInclude(g => g.StudentGroupCompositions)
                    .SelectMany(a => a.Group!.StudentGroupCompositions)
                    .Select(sgc => sgc.StudentId)
                    .Distinct()
                    .ToListAsync();

                foreach (var studentId in affectedStudentIds)
                {
                    await _hub.Clients
                        .Group($"student_{studentId}")
                        .SendAsync("ScheduleChanged");
                }

                var updatedSchedule = await _context.Schedule
                    .Include(s => s.Assignment!).ThenInclude(a => a.Teacher)
                    .Where(s => s.Assignment!.TeacherId == teacherId)
                    .OrderByDescending(s => s.DateTime)
                    .Take(15)
                    .Select(r => new
                    {
                        r.Id,
                        r.AssignmentId,
                        DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                        r.Status,
                        r.Duration,
                        TeacherId = r.Assignment!.Teacher!.Id,
                        TeacherName = r.Assignment!.Teacher!.Name
                    })
                    .ToListAsync();

                await _hub.Clients
                    .Group($"teacher_{teacherId}")
                    .SendAsync("ScheduleChanged", updatedSchedule);
            }

            return RedirectToAction(nameof(Manage), new
            {
                teacherId,
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }
        
        
    }
}
