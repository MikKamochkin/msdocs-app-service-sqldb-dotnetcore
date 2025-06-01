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
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Execution;


namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "student")]
    public class StudentsController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IHubContext<ZoomMeetingHub> _hub;
        private readonly ILogger<TeachersController> _logger;

        public StudentsController(MyDatabaseContext context, IHubContext<ZoomMeetingHub> hub, ILogger<TeachersController> logger)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
        }

        // GET: Students/Index
        public async Task<IActionResult> Index()
        {
            // Get the current user's ID from their claims
            var userId = User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            // Find the student record associated with this user
            var student = await _context.Student
                .Include(s => s.Contacts)
                //.Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ID.ToString() == userId);

            if (student == null)
                return NotFound();

            var studentId = Guid.Parse(userId);
            ViewBag.Username = await _context.User
                                    .Where(u => u.ID == studentId)
                                    .Select(u => u.Username)
                                    .FirstOrDefaultAsync();


            // 5) Supply your time-zone list again
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var studentsTimezone = await _context.Student
                .Where(s => s.ID == studentId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                if (studentsTimezone != null)
                {
                    tz.Selected = tz.Value == studentsTimezone;
                }
                else
                {
                    tz.Selected = tz.Value == defaultZone;
                }

            }
            ViewBag.TimeZones = timeZones;

            return View(student);
        }

        // GET: /Students/Schedule
        public async Task<IActionResult> Schedule()
        {
            // 1) Get the student ID from their UserID claim
            var userId = User.FindFirst("UserID")?.Value;
            ViewBag.StudentId = userId;
            if (!Guid.TryParse(userId, out var studentId))
                return NotFound();

            // 2) Fetch all GroupIds this student belongs to
            var groupIds = await _context.StudentGroupComposition
                .Where(c => c.StudentId == studentId)
                .Select(c => c.GroupId)
                .Distinct()
                .ToListAsync();

            // 3) Pull every schedule entry for those groups
            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Teacher)
                .Where(s => groupIds.Contains(s.Assignment!.GroupId))
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
                TeacherName = s.Assignment!.Teacher!.Name,
                TeacherId = s.Assignment!.Teacher!.Id,

            });

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flat,
                new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles }
            );

            // 5) Supply your time-zone list again
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var studentsTimezone = await _context.Student
                .Where(s => s.ID == studentId)
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
        public async Task<IActionResult> Zoom(Guid? scheduleId)
        {
            //_logger.LogInformation("In studentscontroller Zoom() called with scheduleId: {sid}", scheduleId);

            if (scheduleId == null)
            {
                return NotFound();
            }
            else
            {
                // 1) Identify the logged-in teacher
                var userIdClaim = User.FindFirst("UserID")?.Value;
                if (!Guid.TryParse(userIdClaim, out var studentId))
                    return NotFound();

                // 2) Fetch the teacher model (for the view)
                var student = await _context.Student
                                    .FirstOrDefaultAsync(t => t.ID == studentId);

                //_logger.LogInformation("In zoom() with student: {s}", student?.Name);
                if (student == null)
                    return NotFound();

                var targetLesson = await _context.Schedule
                    .Where(s => s.Id == scheduleId)
                    .FirstOrDefaultAsync();

                //_logger.LogInformation("target lessons duraion: {a}", targetLesson?.Duration);

                if (targetLesson != null)
                {
                    // b) Lookup the ZoomMeeting by ScheduleId
                    var zoomMeeting = await _context.ZoomMeetings
                        .FirstOrDefaultAsync(z => z.ScheduleId == targetLesson.Id);

                    ViewBag.ZoomLink = zoomMeeting?.JoinUrl;
                    ViewBag.MeetingId = zoomMeeting?.MeetingId;
                    ViewBag.MeetingPassword = zoomMeeting?.MeetingPassword;
                    ViewBag.MeetingStatus = zoomMeeting?.Schedule?.Status;
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

                // 5) Render the same Zoom.cshtml view (which already has
                //    the timing + enable/disable logic)
                return View(student);
            }
        }

        // GET: /Students/ScheduleJson
        [HttpGet]
        public async Task<IActionResult> ScheduleJson()
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var studentId))
                return Unauthorized();

            // same LINQ you already have in Schedule(), minus the View-bag work
            var groupIds = await _context.StudentGroupComposition
                            .Where(c => c.StudentId == studentId)
                            .Select(c => c.GroupId)
                            .Distinct()
                            .ToListAsync();

            var results = await _context.Schedule
                .Include(s => s.Assignment).ThenInclude(a => a.Teacher)
                .Where(s => groupIds.Contains(s.Assignment!.GroupId))
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .Select(s => new {
                    s.Id,
                    DateTime = s.DateTime.ToString("o"),
                    s.Status,
                    s.Duration,
                    TeacherName = s.Assignment!.Teacher!.Name,
                    TeacherId   = s.Assignment!.Teacher!.Id
                })
                .ToListAsync();

            return Ok(results);      // HTTP 200 with JSON
        }


        // 1) Render the “waiting room” page
        [HttpGet]
        public IActionResult Wait(Guid scheduleId)
        {
            return View(model: scheduleId);
        }


        [HttpGet]
        public async Task<IActionResult> GetJoinUrl(Guid scheduleId)
        {
            var zoom = await _context.ZoomMeetings
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId);

            if (zoom == null)
                return NoContent();          // 204 → not ready

            return Ok(new { joinUrl = zoom.JoinUrl });
        }

        [HttpPost]
        public async Task<IActionResult> SaveTimeZone([FromBody] TimeZoneUpdateModel model)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var studentId))
                return NotFound();

            var student = await _context.Student.FirstOrDefaultAsync(s => s.ID == studentId);
            if (student == null)
                return NotFound();

            student.TimeZoneId = model.TimeZoneId;
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