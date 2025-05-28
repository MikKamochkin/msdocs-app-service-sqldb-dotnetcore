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
using DotNetCoreSqlDb.Services;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "teacher")]
    public class TeachersController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<TeachersController> _logger;

        private readonly IZoomMeetingService _svc;

        public TeachersController(MyDatabaseContext context, ILogger<TeachersController> logger, IZoomMeetingService svc)
        {
            _context = context;
            _logger = logger;
            _svc = svc;
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
        public async Task<IActionResult> Zoom(Guid? scheduleId)
        {
            _logger.LogInformation("Zoom() called with scheduleId: {sid}", scheduleId);

            if (scheduleId == null)
            {
                return NotFound();
            }
            else
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

                var targetLesson = await _context.Schedule
                    .Where(s => s.Id == scheduleId)
                    .FirstOrDefaultAsync();

                
                if (targetLesson != null)
                {
                    // b) Lookup the ZoomMeeting by ScheduleId
                    var zoomMeeting = await _context.ZoomMeetings
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

                // 5) Render the same Zoom.cshtml view (which already has
                //    the timing + enable/disable logic)
                return View(teacher);
            }
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

        //Check if there's already a lesson with this teacher, if yes, end that first.
        [HttpGet]
        public async Task<IActionResult> StartLesson(Guid id)
        {
            // 1) get the teacherId for this schedule
            var teacherId = await _context.Schedule
                .Where(s => s.Id == id)
                .Select(s => s.Assignment!.TeacherId)
                .FirstOrDefaultAsync();

            if (teacherId == default)
            {
                _logger.LogWarning("Schedule {ScheduleId} has no Assignment/Teacher", id);
                return NotFound();
            }

            // 2) grab the first ZoomMeeting on any *other* schedule for that same teacher
            var previousLesson = await _context.ZoomMeetings
                .Include(zm => zm.Schedule)
                    .ThenInclude(s => s.Assignment)
                .Where(zm =>
                    zm.Schedule.Assignment!.TeacherId == teacherId &&  // same teacher
                    zm.ScheduleId                      != id &&          // different schedule
                    zm.IsBusy                                     // still busy
                )
                .OrderByDescending(zm => zm.Schedule.DateTime)       // most recent first
                .FirstOrDefaultAsync();

            if (previousLesson != null)
            {
                _logger.LogInformation(
                    "Found previous lesson {PrevLessonId} for teacher {TeacherId}",
                    previousLesson.Id, teacherId);

                // unwrap the nullable ScheduleId
                var prevSchedId = previousLesson.ScheduleId
                    ?? throw new InvalidOperationException("ZoomMeeting.ScheduleId was null");

                await _svc.EndMeetingsAsync(prevSchedId);
            }
            else
            {
                _logger.LogInformation(
                    "No previous busy lesson found for teacher {TeacherId}", 
                    teacherId);
            }

            // 3) now start the new meeting    
            _logger.LogInformation("Called StartLesson for schedule {ScheduleId}", id);
            await _svc.AssignMeetingsAsync(id);

            var lesson = await _context.ZoomMeetings
                .FirstOrDefaultAsync(z => z.ScheduleId == id);
            if (lesson == null) 
                return NotFound();

            return Redirect(lesson.JoinUrl);
        }

        

        [HttpGet]
        public async Task<IActionResult> EndLesson(Guid id)
        {
            _logger.LogInformation("Called end lesson with id: {id}", id);
            
            await _svc.EndMeetingsAsync(id);

            return Ok(new { message = "EndLesson triggered" }); // returns HTTP 200
        }



        /*public async Task<IActionResult> Calendar()
        {return View();}*/

    }
}