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
using DotNetCoreSqlDb.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "teacher")]
    public class TeachersController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<TeachersController> _logger;
        private readonly IZoomMeetingService _zoomSvc;
        private readonly IUpdateBalanceService _balanceSvc;

        public TeachersController(MyDatabaseContext context, ILogger<TeachersController> logger, IZoomMeetingService zoomSvc, IUpdateBalanceService balanceSvc)
        {
            _context = context;
            _logger = logger;
            _zoomSvc = zoomSvc;
            _balanceSvc = balanceSvc;
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

            var user = await _context.User.FirstOrDefaultAsync(u => u.ID.ToString() == userId);

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
            ViewBag.Username = user.Username;

            return View(teacher);
        }

        // GET: /Students/Schedule
        public async Task<IActionResult> Schedule()
        {

            var userId = User.FindFirst("UserID")?.Value;
            ViewBag.TeacherId = userId;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            var sixHoursAgo = DateTime.UtcNow.AddHours(-6);

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .Where(s => s.DateTime > sixHoursAgo)
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .ToListAsync();

            /*var zoomLinks = await _context.ZoomMeetings
                .Where(z => scheduleEntries.Select(s => s.Id).Contains(z.ScheduleId))
                .ToDictionaryAsync(z => z.ScheduleId, z => z.JoinUrl);*/


            // 4) Serialize for the view’s JS (ISO timestamps + teacher names)
            var flat = scheduleEntries.Select(s => new
            {
                s.Id,
                s.AssignmentId,
                DateTime = s.DateTime.ToString("o"),
                s.Status,
                s.Duration,
                StudentName = s.Assignment?.Group?.Name
                //JoinUrl = zoomLinks.TryGetValue(s.Id, out var url) ? url : null
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

            if (User.IsImpersonating())
            {
                ViewBag.Impersonating = true;
            }

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
                    zm.ScheduleId != id &&          // different schedule
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

                await _zoomSvc.EndMeetingsAsync(prevSchedId);
            }
            else
            {
                _logger.LogInformation(
                    "No previous busy lesson found for teacher {TeacherId}",
                    teacherId);
            }

            // 3) now start the new meeting    
            _logger.LogInformation("Called StartLesson for schedule {ScheduleId}", id);
            await _zoomSvc.AssignMeetingsAsync(id);

            var sched = await _context.Schedule
                .FirstOrDefaultAsync(s => s.Id == id);

            //If a schedule was marked as cancelled with pay before lesson starts with StudentCharge = 0, accounted set to true
            //Then, we "uncancel" this lesson, set it back to scheduled, and set StudentCharge = 100, accounted is still true
            //We don't want to charge them again when we start this lesson
            //Otherwise, all scheduled lessons will have accounted == false
            if (sched != null && sched.Accounted == false)
            {
                await _balanceSvc.UpdateStudentBalanceAsync(HttpContext.RequestAborted, id);
            }

            var lesson = await _context.ZoomMeetings
                .FirstOrDefaultAsync(z => z.ScheduleId == id);
            if (lesson == null)
                //TODO: Add error handling here
                return NotFound();

            return Redirect(lesson.JoinUrl);
        }



        [HttpGet]
        public async Task<IActionResult> EndLesson(Guid id)
        {
            _logger.LogInformation("Called end lesson with id: {id}", id);

            await _zoomSvc.EndMeetingsAsync(id);

            return Ok(new { message = "EndLesson triggered" }); // returns HTTP 200
        }


        [HttpGet]
        public async Task<IActionResult> ScheduleJson()
        {
            var userId = User.FindFirst("UserID")?.Value;
            ViewBag.TeacherId = userId;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            var sixHoursAgo = DateTime.UtcNow.AddHours(-6);

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .Where(s => s.DateTime > sixHoursAgo)
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .Select(s => new
                {
                    s.Id,
                    s.AssignmentId,
                    DateTime = s.DateTime.ToString("o"),
                    s.Status,
                    s.Duration,
                    StudentName = s.Assignment.Group.Name
                })
                .ToListAsync();


            /*
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
            */
            return Ok(scheduleEntries);      // HTTP 200 with JSON*/
        }

        [HttpGet]
        public async Task<IActionResult> EditSchedule(DateTime? date)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            // Selected date (defaults to today, local)
            var selectedDate = (date ?? DateTime.Today).Date;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");

            // Time dropdown (5-minute increments to match your Manage page)
            ViewBag.Times = Enumerable.Range(0, 24 * 12)
                .Select(i => TimeSpan.FromMinutes(i * 5))
                .Select(ts => new SelectListItem
                {
                    Value = ts.ToString(@"hh\:mm"),
                    Text = ts.ToString(@"hh\:mm"),
                    Selected = ts == TimeSpan.FromHours(9)
                })
                .ToList();

            // Status dropdown (teachers can set status for new entries; existing remain read-only)
            /*ViewBag.Statuses = DropdownOptions.ScheduleStatusTypes
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "Scheduled"
                });*/

            // Duration dropdown (fixed list as in Manage)
            ViewBag.LessonDurationTypes = DropdownOptions.LessonDurationTypes
                .Select(item => new SelectListItem { Text = item.Text, Value = item.Value })
                .ToList();

            // Accounting type dropdown (use your default S100T100)
            /*ViewBag.LessonAccountingType = DropdownOptions.LessonAccountingType
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "S100T100"
                });*/

            // Time zones
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            foreach (var tz in timeZones) tz.Selected = tz.Value == defaultZoneIana;
            ViewBag.TimeZones = timeZones;

            // Groups visible to this teacher only (value = AssignmentId, text = Group.Name)
            var assignments = await _context.Assignments
                .Where(a => a.TeacherId == teacherId && a.Group!.IsActive)
                .Select(a => new { a.Id, a.GroupId, GroupName = a.Group!.Name })
                .OrderBy(x => x.GroupName)
                .ToListAsync();

            var groupItems = assignments
                .GroupBy(x => x.GroupId)
                .Select(g => g.First())
                .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.GroupName })
                .OrderBy(x => x.Text)
                .ToList();

            ViewBag.Groups = groupItems;

            // AssignmentId -> default StudentUnitDuration map
            var durationMap = await _context.Assignments
                .Where(a => a.TeacherId == teacherId)
                .ToDictionaryAsync(a => a.Id.ToString(), a => a.StudentUnitDuration);
            ViewBag.DurationMapJson = JsonSerializer.Serialize(durationMap);

            // Load existing entries for *that date* (read-only in the view)
            // Convert selected local day → UTC window
            var windowsZoneId   = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo          = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            // IMPORTANT: make the local date "Unspecified" so ConvertTimeToUtc(sourceTz) accepts it
            var selectedDateLocal = DateTime.SpecifyKind(selectedDate.Date, DateTimeKind.Unspecified);

            var localStart = selectedDateLocal;              // 00:00 local (Unspecified)
            var localEnd   = selectedDateLocal.AddDays(1);   // next midnight (Unspecified)

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
            var endUtc   = TimeZoneInfo.ConvertTimeToUtc(localEnd,   tzInfo);

            var existing = await _context.Schedule
                .Include(s => s.Assignment!).ThenInclude(a => a.Group!)
                .Where(s => s.Assignment!.TeacherId == teacherId &&
                            s.DateTime >= startUtc && s.DateTime < endUtc)
                .OrderBy(s => s.DateTime)
                .ToListAsync();

            var flat = existing.Select(r => new
            {
                r.Id,
                r.AssignmentId,
                DateTime = r.DateTime.ToUniversalTime().ToString("o"),     
                r.Duration,
                GroupName = r.Assignment!.Group!.Name,
                IsExisting = true  // important: disables edit/delete in the view
            }).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(flat, new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });

            return View("EditSchedule", existing);
        }

        // keep this DTO local to TeachersController so you don't depend on ScheduleController
        public class ScheduleDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public DateTime DateTime { get; set; }
            public int Duration { get; set; }
        }

        // Teachers can only create new rows; if Id is present, we reject it
        [HttpPost]
        public async Task<IActionResult> SaveEntry([FromBody] ScheduleDto dto)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return Unauthorized();

            if (dto == null)
                return BadRequest("Invalid payload");

            if (dto.Id != Guid.Empty)
                return BadRequest("Editing existing entries is not allowed.");

            // Ensure the assignment belongs to this teacher
            var assignment = await _context.Assignments
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.Id == dto.AssignmentId && a.TeacherId == teacherId);

            if (assignment == null)
                return BadRequest("Invalid assignment for this teacher.");

            var entity = new Schedule
            {
                Id = Guid.NewGuid(),
                AssignmentId = dto.AssignmentId,
                DateTime = dto.DateTime.Kind == DateTimeKind.Local ? dto.DateTime.ToUniversalTime() : dto.DateTime,
                Status = "Scheduled",
                Duration = dto.Duration,
                LessonAccountingType = "S100T100",
                Accounted = false
            };

            _context.Schedule.Add(entity);
            await _context.SaveChangesAsync();

            // Return a row that will render as read-only (IsExisting = true)
            return Json(new
            {
                id = entity.Id,
                assignmentId = entity.AssignmentId,
                dateTime = entity.DateTime.ToUniversalTime().ToString("o"),
                duration = entity.Duration,
                groupName = assignment.Group?.Name ?? "(unknown)",
                isExisting = true
            });
        }
    }
}