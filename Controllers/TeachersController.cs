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

        // GET: /Students/Zoom
        public async Task<IActionResult> Zoom()
        {
            // 1) Identify the logged-in student
            var userIdClaim = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userIdClaim, out var teacherId))
                return NotFound();

            // 2) Fetch the student model (needed for the view)
            var teacher = await _context.Teacher               
                                .FirstOrDefaultAsync(s => s.Id == teacherId);

            if (teacher == null)
                return NotFound();

            // 3) Find all group IDs this student belongs to
            /*var groupIds = await _context.StudentGroupComposition
                                .Where(c => c.StudentId == studentId)
                                .Select(c => c.GroupId)
                                .Distinct()
                                .ToListAsync();*/

            // 4) Pick the next/ongoing lesson (within 3h behind → future)
            // TODO: fix nowutc.addhours(-3)
            var nowUtc = DateTime.UtcNow;
            var targetLesson = await _context.Schedule
                .Include(s => s.Assignment)
                .Where(s =>
                    s.Id == teacherId &&
                    s.DateTime >= nowUtc.AddHours(-3))
                .OrderBy(s => s.DateTime)
                .FirstOrDefaultAsync();

            // 5) Surface the lesson’s start time + duration
            if (targetLesson != null)
            {
                ViewBag.LessonUtc = DateTime.SpecifyKind(targetLesson.DateTime, DateTimeKind.Utc)
                                        .ToUniversalTime()
                                        .ToString("o");
                ViewBag.LessonDuration = targetLesson.Duration;

                // 6) Only now look up the ZoomMeeting by ScheduleId
                var zoomMeeting = await _context.ZoomMeetings
                                    .Include(z => z.Schedule)
                                    .FirstOrDefaultAsync(z => z.ScheduleId == targetLesson.Id);

                ViewBag.ZoomLink = zoomMeeting?.JoinUrl;
                ViewBag.MeetingId = zoomMeeting?.MeetingId;
                ViewBag.MeetingPassword = zoomMeeting?.MeetingPassword;
            }
            else
            {
                // no upcoming lesson
                ViewBag.LessonUtc = string.Empty;
                ViewBag.LessonDuration = null;
                ViewBag.ZoomLink = null;
                ViewBag.MeetingId = null;
                ViewBag.MeetingPassword = null;
            }

            // 7) Finally, render the view with your student model
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