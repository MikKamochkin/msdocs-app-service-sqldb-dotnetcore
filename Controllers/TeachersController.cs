using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using TimeZoneConverter;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "teacher")]
    public class TeachersController : Controller
    {
        private readonly MyDatabaseContext _context;

        public TeachersController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Teacher/Index
        public async Task<IActionResult> Index()
        {

            var userId = User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var teacher = await _context.Teacher
                //.Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id.ToString() == userId);

            if (teacher == null)
            {
                return NotFound();
            }

            var teacherId = Guid.Parse(userId);

            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var teachersTimezone = await _context.Teacher
                .Where(s => s.Id == teacherId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                if (teachersTimezone != null)
                {
                    tz.Selected = tz.Value == teachersTimezone;
                }
                else
                {
                    tz.Selected = tz.Value == defaultZone;
                }

            }
            ViewBag.TimeZones = timeZones;

            return View(teacher);
        }

        // GET: /Students/Schedule
        public async Task<IActionResult> Schedule()
        {

            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .ToListAsync();

            // 4) Serialize for the view’s JS (ISO timestamps + teacher names)
            var flat = scheduleEntries.Select(s => new
            {
                s.Id,
                s.AssignmentId,
                DateTime = s.DateTime.ToString("o"),
                s.Status,
                s.Duration,
                StudentName = s.Assignment?.Group?.Name
            });

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flat,
                new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles }
            );

            // 5) Supply your time-zone list again
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var studentsTimezone = await _context.Teacher
                .Where(s => s.Id == teacherId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == studentsTimezone;
            }
            ViewBag.TimeZones = timeZones;

            // 6) Pass the raw schedule entries into the Razor view
            return View(scheduleEntries);
        }

        // GET: /Teachers/Zoom
        public async Task<IActionResult> Zoom()
        {
            // 1) Identify the logged-in teacher
            var userIdClaim = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userIdClaim, out var teacherId))
                return NotFound();

            // 2) Fetch the teacher model (for the view)
            var teacher = await _context.Teacher
                                .FirstOrDefaultAsync(t => t.Id == teacherId);
            if (teacher == null)
                return NotFound();

            // 3) Pick the next/ongoing lesson (within 3h behind → future),
            //    filtering by Assignment.TeacherId
            var nowUtc = DateTime.UtcNow;
            var targetLesson = await _context.Schedule
                .Include(s => s.Assignment)
                .Where(s =>
                    s.Assignment != null &&
                    s.Assignment.TeacherId == teacherId &&
                    s.DateTime >= nowUtc.AddHours(-3))
                .OrderBy(s => s.DateTime)
                .FirstOrDefaultAsync();

            // 4) If we found a lesson, surface its time, duration, and Zoom link
            if (targetLesson != null)
            {
                // a) Lesson start time (UTC) for the JS
                ViewBag.LessonUtc = DateTime
                    .SpecifyKind(targetLesson.DateTime, DateTimeKind.Utc)
                    .ToUniversalTime()
                    .ToString("o");
                ViewBag.LessonDuration = targetLesson.Duration;

                // b) Lookup the ZoomMeeting by ScheduleId
                var zoomMeeting = await _context.ZoomMeetings
                    .FirstOrDefaultAsync(z => z.ScheduleId == targetLesson.Id);

                ViewBag.ZoomLink       = zoomMeeting?.JoinUrl;
                ViewBag.MeetingId      = zoomMeeting?.MeetingId;
                ViewBag.MeetingPassword= zoomMeeting?.MeetingPassword;
            }
            else
            {
                // no upcoming lesson
                ViewBag.LessonUtc        = string.Empty;
                ViewBag.LessonDuration   = null;
                ViewBag.ZoomLink         = null;
                ViewBag.MeetingId        = null;
                ViewBag.MeetingPassword  = null;
            }

            // 5) Render the same Zoom.cshtml view (which already has
            //    the timing + enable/disable logic)
            return View(teacher);
        }

        
        [HttpPost]
        public async Task<IActionResult> SaveTimeZone([FromBody] TimeZoneUpdateModel model)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            var teacher = await _context.Teacher.FirstOrDefaultAsync(s => s.Id == teacherId);
            if (teacher == null)
                return NotFound();

            teacher.TimeZoneId = model.TimeZoneId;
            await _context.SaveChangesAsync();

            return Ok();
        }

        public class TimeZoneUpdateModel
        {
            public string TimeZoneId { get; set; }
        }



        /*public async Task<IActionResult> Calendar()
        {return View();}*/

    }
}