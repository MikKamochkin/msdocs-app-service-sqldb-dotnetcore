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
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "teacher")]
    public class TeachersController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<TeachersController> _logger;
        private readonly IZoomMeetingService _zoomSvc;
        private readonly IUpdateBalanceService _balanceSvc;
        private readonly IHubContext<ScheduleHub> _hub;
        private readonly IConversationService _conversationService;
        private readonly IBlobService _blobService;
        private readonly LogHelper _logHelper;
        private const int JoinEarlyMinutes = 5;
        private const int GraceAfterMinutes = 90;

        public TeachersController(
            MyDatabaseContext context,
            ILogger<TeachersController> logger,
            IZoomMeetingService zoomSvc,
            IUpdateBalanceService balanceSvc,
            IHubContext<ScheduleHub> hub,
            IConversationService conversationService,
            IBlobService blobService,
            LogHelper logHelper)
        {
            _context = context;
            _logger = logger;
            _zoomSvc = zoomSvc;
            _balanceSvc = balanceSvc;
            _hub = hub;
            _logHelper = logHelper;
            _conversationService = conversationService;
            _blobService = blobService;
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

            if (user == null)
            {
                return NotFound();
            }

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
            var utcNow = DateTime.UtcNow;

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .Where(s => s.DateTime > sixHoursAgo)
                .Where(s => s.Status != "Deleted")
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

            // 4) Serialize for the view’s JS (ISO timestamps + teacher names)
            /*var flat = scheduleEntries.Select(s => new
            {
                s.Id,
                s.AssignmentId,
                DateTime = s.DateTime.ToString("o"),
                s.Status,
                s.Duration,
                StudentName = s.Assignment?.Group?.Name
                //JoinUrl = zoomLinks.TryGetValue(s.Id, out var url) ? url : null
            });*/

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flatDtos,
                new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles }
            );

            // 5) Supply your time-zone list again
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var teachersTimezone = await _context.Teacher
                .Where(s => s.Id == teacherId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == teachersTimezone;
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
            public string TimeZoneId { get; set; } = string.Empty;
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
                .Include(zm => zm.Schedule!)
                    .ThenInclude(s => s.Assignment)
                .Where(zm =>
                    zm.Schedule!.Assignment!.TeacherId == teacherId &&  // same teacher
                    zm.ScheduleId != id &&          // different schedule
                    zm.IsBusy                                     // still busy
                )
                .OrderByDescending(zm => zm.Schedule!.DateTime)       // most recent first
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
                await _logHelper.LogZoomMeetingEndAsync(prevSchedId);
            }
            else
            {
                _logger.LogInformation(
                    "No previous busy lesson found for teacher {TeacherId}",
                    teacherId);
            }

            // 3) now start the new meeting    
            //_logger.LogInformation("Called StartLesson for schedule {ScheduleId}", id);
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

            bool existsInZoomMeetingLog = await _context.ZoomMeetingLog
                .AsNoTracking()
                .Where(zml => zml.ScheduleId == id)
                .AnyAsync();

            if (!existsInZoomMeetingLog)
            {
                await _logHelper.LogZoomMeetingStartAsync(id, lesson.Id);
            }

            await _hub.Clients
                .Group($"teacher_{teacherId}")
                .SendAsync("LessonStarted");

            return Redirect(lesson.JoinUrl!);
        }

        [HttpGet]
        public async Task<IActionResult> EndLesson(Guid id)
        {
            await _zoomSvc.EndMeetingsAsync(id);
            await _logHelper.LogZoomMeetingEndAsync(id);

            var schedule = await _context.Schedule
                .Include(s => s.Assignment!)
                    .ThenInclude(a => a.Group)
                        .ThenInclude(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(s => s.Id == id);

            
            if (schedule == null)
            {
                return NotFound();
            }
            
            var affectedStudentIds = schedule.Assignment?.Group?.StudentGroupCompositions?
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToList() ?? new List<Guid>();

            foreach (var studentId in affectedStudentIds)
            {
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }

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
            var utcNow = DateTime.UtcNow;

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .Where(s => s.DateTime > sixHoursAgo)
                .Where(s => s.Status != "Deleted")
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

            return Ok(new ScheduleJsonResponse
            {
                Rows = flatDtos,
                NextRefreshMs = timeUntilSoonest > 0 ? (long)timeUntilSoonest : null
            });

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
            //return Ok(scheduleEntries);      // HTTP 200 with JSON*/
        }

        [HttpGet]
        public async Task<IActionResult> EditSchedule(DateTime? date)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            ViewBag.TeacherId = teacherId;

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
            var teachersTimezone = await _context.Teacher
                .Where(s => s.Id == teacherId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == teachersTimezone;
            }
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
            var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            // IMPORTANT: make the local date "Unspecified" so ConvertTimeToUtc(sourceTz) accepts it
            var selectedDateLocal = DateTime.SpecifyKind(selectedDate.Date, DateTimeKind.Unspecified);

            var localStart = selectedDateLocal;              // 00:00 local (Unspecified)
            var localEnd = selectedDateLocal.AddDays(1);   // next midnight (Unspecified)

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tzInfo);

            var existing = await _context.Schedule
                .Include(s => s.Assignment!).ThenInclude(a => a.Group!)
                .Where(s => s.Assignment!.TeacherId == teacherId &&
                            s.DateTime >= startUtc && s.DateTime < endUtc && s.Status != "Deleted")
                .OrderBy(s => s.DateTime)
                .ToListAsync();

            // Map your existing Schedule models to DTOs
            var flat = existing.Select(r => new ScheduleEditDto
            {
                Id = r.Id,
                DateTime = r.DateTime.ToUniversalTime(),   // Keep as DateTime, formatting happens in JS if needed
                Duration = r.Duration,
                GroupName = r.Assignment!.Group!.Name,
                IsExisting = true,                         // same as before
                CanDelete = !r.HasReachedMinimumDuration ?? false,                         // explicit field in DTO
                Status = r.Status
            }).ToList();

            // Serialize for your JS
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

        public class ScheduleEditDto
        {
            public Guid Id { get; set; }
            public DateTime DateTime { get; set; }
            public int Duration { get; set; }
            public string GroupName { get; set; } = "";
            public bool IsExisting { get; set; }
            public bool CanDelete { get; set; }
            public string Status { get; set; } = "";
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
                    .ThenInclude(a => a.StudentGroupCompositions)
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
                Accounted = false,
                HasReachedMinimumDuration = false
            };

            _context.Schedule.Add(entity);

            var affectedStudentIds = entity.Assignment?.Group?.StudentGroupCompositions?
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToList() ?? new List<Guid>();


            await _context.SaveChangesAsync();

            _logger.LogInformation("studentid affected: " + affectedStudentIds.Count);
            foreach (var studentId in affectedStudentIds)
            {
                _logger.LogInformation("studentid affected: " + studentId);
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }

            // Return a row that will render as read-only (IsExisting = true)
            return Json(new
            {
                id = entity.Id,
                assignmentId = entity.AssignmentId,
                dateTime = entity.DateTime.ToUniversalTime().ToString("o"),
                duration = entity.Duration,
                groupName = assignment.Group?.Name ?? "(unknown)",
                isExisting = true,
                hasReachedMinimumDuration = false,
                status = entity.Status
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEntry([FromBody] Guid id)
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            // Load + ensure ownership
            var entity = await _context.Schedule
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Group)
                        .ThenInclude(g => g.StudentGroupCompositions)
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Teacher)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity == null)
                return NotFound();

            if (entity.Assignment?.TeacherId != teacherId)
                return NotFound();


            if (entity.HasReachedMinimumDuration == true)
            {
                return BadRequest("This lesson cannot be deleted");
            }

            if (entity.Status == "Ongoing")
            {
                return BadRequest("An ongoing lesson cannot be deleted");
            }

            // Collect affected IDs before delete
            var affectedStudentIds = entity.Assignment?.Group?.StudentGroupCompositions?
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToList() ?? new List<Guid>();

            bool existsInZoomMeetingLog = await _context.ZoomMeetingLog.AnyAsync(z => z.ScheduleId == entity.Id);
            if (!existsInZoomMeetingLog)
            {
                _context.Schedule.Remove(entity);
            }
            else
            {
                entity.Status = "Deleted";
            }
            
            await _context.SaveChangesAsync();

            // Notify students
            foreach (var studentId in affectedStudentIds)
            {
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }

            // Send refreshed list to teacher (latest 15)
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

            return Ok();
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
                DateTime = startUtc,
                Status = s.Status ?? "",
                Duration = s.Duration,
                StudentName = s.Assignment?.Group?.Name,
                EnableAtUtc = enableAtUtc,
                DisableAtUtc = disableAtUtc,
                CanJoinNow = canJoinNow,
                HighlightRow = highlightRow
            };
        }

        public async Task<IActionResult> LessonHistory(int week = 0)
        {
            _logger.LogInformation("week: " + week);
            var userId = User.FindFirst("UserID")?.Value;
            ViewBag.TeacherId = userId;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            var utcNow = DateTime.UtcNow;
            DateTime beginningOfWeek = utcNow.Date.AddDays(-(int)(utcNow.DayOfWeek - DayOfWeek.Monday + 7) % 7);
            DateTime mondayWithWeekOffset = beginningOfWeek.AddDays(week * 7);
            DateTime sundayWithWeekOffset = mondayWithWeekOffset.AddDays(7);
            _logger.LogInformation("monday: " + mondayWithWeekOffset);

            var scheduleEntries = await _context.Schedule
                .Include(s => s.Assignment)          // load the Assignment nav
                    .ThenInclude(a => a.Group)      // then load its Group nav
                .Where(s => s.Assignment!.TeacherId == teacherId)
                .Where(s => s.DateTime > mondayWithWeekOffset && s.DateTime < sundayWithWeekOffset)
                .Where(s => s.Status == "Taken")
                .OrderByDescending(s => s.DateTime)
                .ToListAsync();

            var flatDtos = scheduleEntries.Select(s => LessonHistoryToDto(s)).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(
                flatDtos,
                new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles }
            );

            // 5) Supply your time-zone list again
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZone = TZConvert.WindowsToIana("Eastern Standard Time");
            var teachersTimezone = await _context.Teacher
                .Where(s => s.Id == teacherId)
                .Select(s => s.TimeZoneId)
                .FirstOrDefaultAsync();

            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == teachersTimezone;
            }
            ViewBag.TimeZones = timeZones;

            if (User.IsImpersonating())
            {
                ViewBag.Impersonating = true;
            }
            ViewBag.Week = week;
            ViewBag.Monday = mondayWithWeekOffset;
            ViewBag.Sunday = sundayWithWeekOffset;
            return View(scheduleEntries);
        }

        [HttpPost]
        public async Task<IActionResult> CopyLastWeek(Guid teacherId, DateTime selectedDate)
        {
            
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var currentTeacherId))
                return Unauthorized();

            teacherId = currentTeacherId;

            //_logger.LogInformation("copy last week for {a}, teacher: {b}", selectedDate, teacherId);
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
                //_logger.LogInformation("entry: {a}", entry.Id);
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
                    Status = "Scheduled",
                    Duration = (int)entry.Assignment!.StudentUnitDuration,
                    LessonAccountingType = "S100T100",
                    Accounted = false
                };

                newEntries.Add(copy);
                _context.Schedule.Add(copy);
            }
            //_logger.LogInformation("new entries count {}", newEntries.Count);
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

            return RedirectToAction(nameof(EditSchedule), new
            {
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }

        private LessonHistoryDto LessonHistoryToDto(Schedule s)
        {
            return new LessonHistoryDto
            {
                Id = s.Id,
                AssignmentId = s.AssignmentId,
                DateTime = s.DateTime,
                Status = s.Status ?? "",
                Duration = s.Duration,
                StudentName = s.Assignment?.Group?.Name
            };
        }

        public sealed class ScheduleRowDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public DateTime DateTime { get; set; }       
            public string Status { get; set; } = "";
            public int Duration { get; set; }
            public string StudentName { get; set; } = "";
            public DateTime EnableAtUtc { get; set; }
            public DateTime DisableAtUtc { get; set; }
            public bool CanJoinNow { get; set; }
            public bool HighlightRow { get; set; }
        }
        public sealed class ScheduleJsonResponse
        {
            public List<ScheduleRowDto> Rows { get; set; } = new();
            public long? NextRefreshMs { get; set; }
        }

        public sealed class LessonHistoryDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public DateTime DateTime { get; set; }       
            public string Status { get; set; } = "";
            public int Duration { get; set; }
            public string StudentName { get; set; } = "";

        }


        [HttpGet]
        public async Task<IActionResult> Conversations()
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            ViewBag.TeacherId = teacherId;

            var conversations = await _context.Conversations
                .Include(c => c.Student)
                .Where(c => c.TeacherId == teacherId && c.IsActive)
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            ViewBag.Conversations = conversations;

            return View();
        }

        public async Task<IActionResult> Chat()
        {
            var userId = User.FindFirst("UserID")?.Value;
            if (!Guid.TryParse(userId, out var teacherId))
                return NotFound();

            ViewBag.TeacherId = teacherId;

            var conversations = await _context.Conversations
                .Include(c => c.Student)
                .Where(c => c.TeacherId == teacherId && c.IsActive)
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            ViewBag.Conversations = conversations;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(Guid conversationId)
        {
            var userId = User.FindFirst("UserID")?.Value;

            if (!Guid.TryParse(userId, out var teacherId))
                return Unauthorized();

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    c.Id == conversationId &&
                    c.TeacherId == teacherId &&
                    c.IsActive);

            if (conversation == null)
                return NotFound();

            var messages = await _context.Messages
                .Include(m => m.Attachments)
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.TimeSent)
                .ToListAsync();

            var latestMessage = messages
                .OrderByDescending(m => m.TimeSent)
                .FirstOrDefault();

            if (latestMessage != null)
            {
                conversation.LastMessageSeenByTeacherId = latestMessage.Id;
                await _context.SaveChangesAsync();
            }

            return Json(messages.Select(m => new
            {
                id = m.Id,
                senderName = m.SenderId == teacherId ? "You" : m.SenderId.ToString(),
                senderId = m.SenderId,
                text = m.Text,
                timeSent = m.TimeSent.ToString("g"),

                attachments = m.Attachments.Select(a => new
                {
                    id = a.Id,
                    originalFileName = a.OriginalFileName,
                    contentType = a.ContentType,
                    sizeBytes = a.SizeBytes,
                    downloadUrl = Url.Action(
                        "DownloadAttachment",
                        "Teachers",
                        new { attachmentId = a.Id })
                })
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
            [FromForm] Guid conversationId,
            [FromForm] Guid senderId,
            [FromForm] string? message,
            [FromForm] IFormFile? file)
        {
            var userId = User.FindFirst("UserID")?.Value;

            if (!Guid.TryParse(userId, out var teacherId))
                return Unauthorized();

            if (senderId != teacherId)
                return Forbid();

            var conversationExists = await _context.Conversations
                .AnyAsync(c =>
                    c.Id == conversationId &&
                    c.TeacherId == teacherId &&
                    c.IsActive);

            if (!conversationExists)
                return Forbid();

            var savedMessage = await _conversationService.SendMessageAsync(
                conversationId,
                senderId,
                message,
                file);

            if (savedMessage == null)
                return NotFound();

            return Ok(new
            {
                messageId = savedMessage.Id
            });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(Guid attachmentId)
        {
            var userId = User.FindFirst("UserID")?.Value;

            if (!Guid.TryParse(userId, out var teacherId))
                return Unauthorized();

            var attachment = await _context.MessageAttachment
                .Include(a => a.Message)
                    .ThenInclude(m => m!.Conversation)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null || attachment.Message?.Conversation == null)
                return NotFound();

            var conversation = attachment.Message.Conversation;

            if (conversation.TeacherId != teacherId)
                return Forbid();

            var downloadResult = await _blobService.DownloadAttachmentAsync(attachment.BlobName);

            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;

            return File(
                downloadResult.Content.ToStream(),
                contentType,
                attachment.OriginalFileName);
        }

    }
}