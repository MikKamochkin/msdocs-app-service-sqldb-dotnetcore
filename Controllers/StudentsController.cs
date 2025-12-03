using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using TimeZoneConverter;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Execution;
using NuGet.Common;
using DotNetCoreSqlDb.ViewModels;
using Twilio.TwiML.Voice;



namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "student")]
    public class StudentsController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IHubContext<ZoomMeetingHub> _hub;
        private readonly ILogger<TeachersController> _logger;

        private const int JoinEarlyMinutes = 5;    // allow join this many minutes before start
        private const int GraceAfterMinutes = 90;

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

            var groupIds = await _context.StudentGroupComposition
                .Where(c => c.StudentId == studentId)
                .Select(c => c.GroupId)
                .Distinct()
                .ToListAsync();

            var assignments = await _context.Assignments
                .Include(a => a.Group)
                .Include(t => t.Teacher)
                .Where(a => groupIds.Contains(a.GroupId))
                .Distinct()
                .ToListAsync();

            var balances = await _context.StudentBalance
                .Where(b => b.StudentId == student.ID)
                .Include(a => a.Assignment)
                    .ThenInclude(t => t.Teacher)
                .Include(b => b.Assignment)
                    .ThenInclude(a => a.Group)
                .ToListAsync();

            ViewBag.Balances = balances;

            ViewBag.Assignments = assignments;


            return View(student);
        }

        public class UpdateInvitationModel
        {
            public Guid ContactId { get; set; }
            public bool Invitation { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInvitationPreference([FromBody] UpdateInvitationModel model)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var studentId))
                return Unauthorized();

            // Make sure the contact belongs to the currently logged-in student
            var contact = await _context.Contact
                .FirstOrDefaultAsync(c => c.ID == model.ContactId && c.StudentID == studentId);

            if (contact == null)
                return NotFound();

            contact.Invitation = model.Invitation;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
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

            var sixHoursAgo = DateTime.UtcNow.AddHours(-6);
            var utcNow = DateTime.UtcNow;

            // 3) Pull every schedule entry for those groups
            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Teacher)
                .Where(s => groupIds.Contains(s.Assignment!.GroupId))
                .Where(s => s.DateTime > sixHoursAgo)
                .OrderBy(s => s.DateTime)
                .Take(15)
                .ToListAsync();

            var soonestLessonTime = DateTime.MaxValue;
            var upcomingLessonEndTime = DateTime.MaxValue;

            foreach (var lesson in scheduleEntries)
            {
                var startTime = lesson.DateTime.AddMinutes(-JoinEarlyMinutes);
                var endTime = lesson.DateTime.AddMinutes(lesson.Duration + GraceAfterMinutes);

                if (startTime < soonestLessonTime && startTime > utcNow)
                {
                    soonestLessonTime = startTime;
                }
                if(endTime < upcomingLessonEndTime && endTime > utcNow)
                {
                    upcomingLessonEndTime = endTime;
                }
            }
            var timeUntilSoonest = Math.Min((soonestLessonTime - DateTime.UtcNow).TotalMilliseconds, (upcomingLessonEndTime - DateTime.UtcNow).TotalMilliseconds) ;


            var flatDtos = scheduleEntries.Select(s => ToDto(s, utcNow)).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flatDtos,
                new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles });

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

        public async Task<IActionResult> LessonAndTransactionHistory()
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var studentId))
                return NotFound();

            // Student TZ (fallback to America/Toronto if missing)
            var studentTzId = await _context.Student
                .Where(s => s.ID == studentId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            var tzInfo = TZConvert.GetTimeZoneInfo(
                string.IsNullOrWhiteSpace(studentTzId) ? "America/Toronto" : studentTzId
            );

            var now = DateTime.UtcNow;
            var sixMonthsAgo = now.AddMonths(-6);

            // 1) Groups for this student
            var groupIds = await _context.StudentGroupComposition
                .Where(gc => gc.StudentId == studentId)
                .Select(gc => gc.GroupId)
                .Distinct()
                .ToListAsync();

            // 2) Assignments in those groups
            var assignmentIds = await _context.Assignments
                .Where(a => groupIds.Contains(a.GroupId))
                .Select(a => a.Id)
                .ToListAsync();

            // 3) Lessons in the last 6 months (past only)
            var lessons = await _context.Schedule
                .AsNoTracking()
                .Where(s => assignmentIds.Contains(s.AssignmentId)
                         && s.DateTime >= sixMonthsAgo
                         && s.DateTime < now)
                .Select(s => new
                {
                    s.Id,
                    s.AssignmentId,
                    s.DateTime,
                    s.Status,
                    s.Duration,
                    TeacherName = s.Assignment.Teacher != null ? s.Assignment.Teacher.Name : null
                })
                .ToListAsync();

            // 4) Transactions attached to lessons (automatic charges)
            var txAttached = await _context.StudentBalanceTransactionLog
                .AsNoTracking()
                .Where(t => t.StudentId == studentId
                         && t.DateTime >= sixMonthsAgo
                         && t.ScheduleId != null)
                .Select(t => new
                {
                    t.Id,
                    t.ScheduleId,
                    t.DateTime,
                    t.TransactionAmount,
                    t.Currency,
                    t.PaymentType,
                    TeacherName = t.Assignment != null && t.Assignment.Teacher != null
                        ? t.Assignment.Teacher.Name
                        : null
                })
                .ToListAsync();

            // If (for any reason) multiple tx rows point at the same schedule, take the most recent
            var txBySchedule = txAttached
                .GroupBy(t => t.ScheduleId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.DateTime ?? DateTime.MinValue).First()
                );

            // 5) Manual / standalone payments (no ScheduleId)
            var txManual = await _context.StudentBalanceTransactionLog
                .AsNoTracking()
                .Where(t => t.StudentId == studentId
                         && t.DateTime >= sixMonthsAgo
                         && t.ScheduleId == null)
                .Select(t => new
                {
                    t.Id,
                    t.DateTime,
                    t.TransactionAmount,
                    t.Currency,
                    t.PaymentType,
                    TeacherName = t.Assignment != null && t.Assignment.Teacher != null
                        ? t.Assignment.Teacher.Name
                        : null
                })
                .ToListAsync();

            // 6) Build merged lesson rows (lesson + its auto charge, if present)
            var mergedLessonRows = lessons.Select(l =>
            {
                txBySchedule.TryGetValue(l.Id, out var tx);

                return new TimelineItem
                {
                    DateTime = TimeZoneInfo.ConvertTimeFromUtc(l.DateTime, tzInfo),
                    Kind = "Lesson",                       // merged row
                    ScheduleId = l.Id,
                    TransactionId = tx?.Id,                // filled if auto-charged
                    AssignmentId = l.AssignmentId,
                    Status = l.Status,
                    DurationMinutes = l.Duration,
                    Amount = tx?.TransactionAmount,
                    Currency = tx?.Currency,
                    PaymentType = tx?.PaymentType,
                    TeacherName = l.TeacherName ?? tx?.TeacherName
                };
            });

            // 7) Build manual payment rows (standalone)
            var manualPaymentRows = txManual.Select(t => new TimelineItem
            {
                DateTime = t.DateTime.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(t.DateTime.Value, tzInfo)
                    : (DateTime?)null,
                Kind = "Payment",                  // distinguish if you like; or "Transaction"
                ScheduleId = null,                 // standalone
                TransactionId = t.Id,
                AssignmentId = null,
                Status = null,
                DurationMinutes = null,
                Amount = t.TransactionAmount,
                Currency = t.Currency,
                PaymentType = t.PaymentType,
                TeacherName = t.TeacherName
            });

            // 8) Final unified timeline: merged lessons + manual payments
            var timeline = mergedLessonRows
                .Concat(manualPaymentRows)
                .OrderByDescending(i => i.DateTime)
                .ToList();

            return View(timeline);
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

            var sixHoursAgo = DateTime.UtcNow.AddHours(-6);
            var utcNow = DateTime.UtcNow;

            var entries = await _context.Schedule
                .Include(s => s.Assignment)!
                    .ThenInclude(a => a.Teacher)
                .Where(s => groupIds.Contains(s.Assignment!.GroupId))
                .Where(s => s.DateTime > sixHoursAgo)
                .OrderBy(s => s.DateTime)
                .Take(15)
                .ToListAsync();

            var soonestLessonTime = DateTime.MaxValue;
            var upcomingLessonEndTime = DateTime.MaxValue;

            foreach (var lesson in entries)
            {
                //if (lesson.Status != "Ongoing" || lesson.Status != "Scheduled") continue;
                var startTime = lesson.DateTime.AddMinutes(-JoinEarlyMinutes);
                var endTime = lesson.DateTime.AddMinutes(lesson.Duration + GraceAfterMinutes);

                if (startTime < soonestLessonTime && startTime > utcNow)
                {
                    soonestLessonTime = startTime;
                }
                if(endTime < upcomingLessonEndTime && endTime > utcNow)
                {
                    upcomingLessonEndTime = endTime;
                }
            }
            var timeUntilSoonest = Math.Min((soonestLessonTime - DateTime.UtcNow).TotalMilliseconds, (upcomingLessonEndTime - DateTime.UtcNow).TotalMilliseconds) ;

            var dtos = entries.Select(s => ToDto(s, utcNow)).ToList();

            return Ok(new ScheduleJsonResponse
            {
                Rows = dtos,
                NextRefreshMs = timeUntilSoonest > 0 ? (long)timeUntilSoonest : null
            });
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

        private ScheduleRowDto ToDto(Schedule s, DateTime utcNow)
        {
            // All times in UTC on the server
            var startUtc = DateTime.SpecifyKind(s.DateTime, DateTimeKind.Utc);
            var enableAtUtc = startUtc.AddMinutes(-JoinEarlyMinutes);
            var disableAtUtc = startUtc.AddMinutes(s.Duration + GraceAfterMinutes);
            var soon = (enableAtUtc - utcNow).TotalHours < 12;
            bool isActiveStatus = string.Equals(s.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(s.Status, "Ongoing", StringComparison.OrdinalIgnoreCase);

            var canJoinNow = isActiveStatus && utcNow >= enableAtUtc && utcNow <= disableAtUtc;
            var highlightRow = isActiveStatus && soon;

            return new ScheduleRowDto
            {
                Id = s.Id,
                AssignmentId = s.AssignmentId,
                // Preserve original field your JS expects:
                DateTime = startUtc, // serialized as ISO string
                Status = s.Status ?? "",
                Duration = s.Duration,
                TeacherName = s.Assignment?.Teacher?.Name ?? "",
                TeacherId = s.Assignment?.Teacher?.Id ?? Guid.Empty,

                // New server-side fields:
                EnableAtUtc = enableAtUtc,
                DisableAtUtc = disableAtUtc,
                CanJoinNow = canJoinNow,
                HighlightRow = highlightRow
            };
        }

        // DTO sent to the view/JS
        public sealed class ScheduleRowDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public DateTime DateTime { get; set; }       // UTC start time (kept for backward compatibility)
            public string Status { get; set; } = "";
            public int Duration { get; set; }
            public string TeacherName { get; set; } = "";
            public Guid TeacherId { get; set; }

            // Server-side timing decisions
            public DateTime EnableAtUtc { get; set; }
            public DateTime DisableAtUtc { get; set; }
            public bool CanJoinNow { get; set; }
            public bool HighlightRow { get; set; }
        }

        public sealed class ScheduleJsonResponse
        {
            public List<ScheduleRowDto> Rows { get; set; } = new();
            public long? NextRefreshMs { get; set; }   // null when nothing upcoming
        }






        /*public async Task<IActionResult> Calendar()
        {return View();}*/
    }
}