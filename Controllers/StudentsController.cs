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
    [Authorize(Roles = "student")]
    public class StudentsController : Controller
    {
        private readonly MyDatabaseContext _context;

        public StudentsController(MyDatabaseContext context)
        {
            _context = context;
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

            return View(student);
        }

        // GET: /Students/Schedule
        public async Task<IActionResult> Schedule()
        {
            // 1) Get the student ID from their UserID claim
            var userId = User.FindFirst("UserID")?.Value;
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
            var flat = scheduleEntries.Select(s => new {
                s.Id,
                s.AssignmentId,
                DateTime    = s.DateTime.ToString("o"),
                s.Status,
                s.Duration,
                TeacherName = s.Assignment!.Teacher!.Name
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

        // GET: /Students/Zoom
        public async Task<IActionResult> Zoom()
        {
            // 1) Identify the logged-in student
            var userIdClaim = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userIdClaim, out var studentId))
                return NotFound();

            // 2) Fetch the student model (needed for the view)
            var student = await _context.Student
                                .Include(s => s.Contacts)
                                .FirstOrDefaultAsync(s => s.ID == studentId);
            if (student == null)
                return NotFound();

            // 3) Find all group IDs this student belongs to
            var groupIds = await _context.StudentGroupComposition
                                .Where(c => c.StudentId == studentId)
                                .Select(c => c.GroupId)
                                .Distinct()
                                .ToListAsync();

            // 4) Pick the next/ongoing lesson (within 3h behind → future)
            // TODO: fix nowutc.addhours(-3)
            var nowUtc = DateTime.UtcNow;
            var targetLesson = await _context.Schedule
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Teacher)
                .Where(s =>
                    groupIds.Contains(s.Assignment!.GroupId) &&
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

                ViewBag.ZoomLink       = zoomMeeting?.JoinUrl;
                ViewBag.MeetingId      = zoomMeeting?.MeetingId;
                ViewBag.MeetingPassword= zoomMeeting?.MeetingPassword;
            }
            else
            {
                // no upcoming lesson
                ViewBag.LessonUtc       = string.Empty;
                ViewBag.LessonDuration  = null;
                ViewBag.ZoomLink        = null;
                ViewBag.MeetingId       = null;
                ViewBag.MeetingPassword = null;
            }

            // 7) Finally, render the view with your student model
            return View(student);
        }



        /*public async Task<IActionResult> Calendar()
        {return View();}*/
    }
}