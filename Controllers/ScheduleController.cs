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
using Microsoft.EntityFrameworkCore.Internal;
using DotNetCoreSqlDb.Services;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin, assistant")]
    public class ScheduleController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IHubContext<ScheduleHub> _hub;
        private readonly ILogger<TeachersController> _logger;
        private readonly IUpdateBalanceService _balanceSvc;
        private readonly IEmailSender _mailer;
        private readonly IZoomMeetingService _zoomSvc;

        public ScheduleController(MyDatabaseContext context, IHubContext<ScheduleHub> hub, ILogger<TeachersController> logger, IUpdateBalanceService balanceSvc, IEmailSender mailer, IZoomMeetingService zoomSvc)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
            _balanceSvc = balanceSvc;
            _mailer = mailer;
            _zoomSvc = zoomSvc;
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
                    Value = item.Value
                })
                .ToList();

            ViewBag.LessonAccountingType = DropdownOptions.LessonAccountingType
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "S100T100"
                });

            // 5) Time zones
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == defaultZoneIana;
            }
            ViewBag.TimeZones = timeZones;

            // Groups + assignment defaults used ONLY when creating a new schedule entry
            List<SelectListItem> groupItems = new();
            var scheduleDefaults = new Dictionary<string, object>();

            if (teacherId.HasValue)
            {
                var assignments = await _context.Assignments
                    .Where(a =>
                        a.TeacherId == teacherId &&
                        a.Group!.IsActive &&
                        a.IsActive)
                    .Select(a => new
                    {
                        a.Id,
                        a.GroupId,
                        GroupName = a.Group!.Name,

                        a.StudentUnitDuration,
                        a.StudentUnitCost,
                        a.StudentUnitType,

                        a.TeacherPayForUnit,
                        a.TeacherPayUnitType
                    })
                    .OrderBy(a => a.GroupName)
                    .ToListAsync();

                // One selectable row per group
                var assignmentsByGroup = assignments
                    .GroupBy(a => a.GroupId)
                    .Select(g => g.First())
                    .ToList();

                groupItems = assignmentsByGroup
                    .Select(a => new SelectListItem
                    {
                        // IMPORTANT: this is now GroupId, NOT AssignmentId
                        Value = a.GroupId.ToString(),
                        Text = a.GroupName
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                // Assignment is only used here to prepopulate a NEW Schedule
                scheduleDefaults = assignmentsByGroup
                    .ToDictionary(
                        a => a.GroupId.ToString(),
                        a => (object)new
                        {
                            AssignmentId = a.Id,
                            Duration = a.StudentUnitDuration,

                            StudentChargeAmount = (int)a.StudentUnitCost,
                            StudentChargeCurrency = a.StudentUnitType,

                            TeacherPayAmount = (int)a.TeacherPayForUnit,
                            TeacherPayCurrency = a.TeacherPayUnitType
                        });
            }

            ViewBag.Groups = groupItems;
            ViewBag.ScheduleDefaultsJson = JsonSerializer.Serialize(scheduleDefaults);
            ViewBag.PayUnits = DropdownOptions.PayUnitTypes;

            // 7) Load existing schedule entries for that teacher + date via UTC range
            var existing = new List<Schedule>();
            if (teacherId.HasValue)
            {
                // Convert selected date (local) bounds into UTC
                var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

                var localStart = new DateTime(
                    selectedDate.Year,
                    selectedDate.Month,
                    selectedDate.Day,
                    0,0,0,
                    DateTimeKind.Unspecified);

                var localEnd = localStart.AddDays(1);

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tzInfo);

                existing = await _context.Schedule
                    .Include(s => s.Group!)
                        .ThenInclude(g => g.StudentGroupCompositions)
                            .ThenInclude(sgc => sgc.Student)
                    .Where(s =>
                        s.TeacherId == teacherId &&
                        s.DateTime >= startUtc &&
                        s.DateTime < endUtc &&
                        s.Status != "Deleted"
                    )
                    .OrderBy(s => s.DateTime)
                    .ToListAsync();
            }

            var studentIds = existing
                .Where(r => r.Group?.StudentGroupCompositions != null)
                .SelectMany(r => r.Group!.StudentGroupCompositions)
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToList();

            var activeNotes = await _context.Set<Notes>()
                .Where(n =>
                    studentIds.Contains(n.StudentID) &&
                    (!n.ExpirationDate.HasValue || n.ExpirationDate.Value >= selectedDate))
                .Select(n => new
                {
                    n.StudentID,
                    n.Value,
                    n.PriorityLevel,
                    n.CreatedDate
                })
                .ToListAsync();

            //_logger.LogInformation("notes: " + activeNotes.Count + " for students: " + studentIds.Count);

            var flat = existing.Select(r =>
            {
                var rowStudentIds = r.Group?.StudentGroupCompositions?
                    .Select(sgc => sgc.StudentId)
                    .ToHashSet()
                    ?? new HashSet<Guid>();

                var rowNotes = activeNotes
                    .Where(n => rowStudentIds.Contains(n.StudentID))
                    .OrderByDescending(n => n.PriorityLevel ?? 0)
                    .ThenByDescending(n => n.CreatedDate)
                    .ToList();

                return new ScheduleRowDto
                {
                    Id = r.Id,
                    GroupId = r.GroupId,
                    TeacherId = r.TeacherId,
                    StudentChargeAmount = r.StudentChargeAmount,
                    StudentChargeCurrency = r.StudentChargeCurrency,
                    TeacherPayAmount = r.TeacherPayAmount,
                    TeacherPayCurrency = r.TeacherPayCurrency,
                    StudentId = rowStudentIds.Count == 1
                        ? rowStudentIds.Single()
                        : null,
                    DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                    Status = r.Status,
                    Duration = r.Duration,
                    Accounted = r.Accounted,
                    LessonAccountingType = r.LessonAccountingType,
                    GroupName = r.Group?.Name ?? string.Empty,
                    MainNotes = string.Join(", ",
                        rowNotes
                            .Where(n => !string.IsNullOrWhiteSpace(n.Value))
                            .Select(n => n.Value)),
                    NotesPriority = rowNotes.Count == 0
                        ? null
                        : rowNotes.Min(n => n.PriorityLevel ?? 0)
                    
                };
            }).ToList();

            ViewBag.ExistingJson = JsonSerializer.Serialize(flat, new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });

            return View(existing);
        }

        public sealed class ScheduleRowDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public Guid GroupId { get; set; }
            public Guid TeacherId { get; set; }
        
            public float StudentChargeAmount { get; set; }
            public string StudentChargeCurrency { get; set; } = "";
            public float TeacherPayAmount { get; set; }
            public string TeacherPayCurrency { get; set; } = "";
            public Guid? StudentId { get; set; }
            public string DateTime { get; set; } = "";
            public string Status { get; set; } = "";
            public int Duration { get; set; }
            public string LessonAccountingType { get; set; } = "";
            public string MainNotes { get; set; } = "";
            public int? NotesPriority { get; set; }
            public bool Accounted { get; set; }
            public string GroupName { get; set; } = "";
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
            //NO LONGER USES TODELETE, INSTEAD USES DELETEENTRY ENDPOINT
            // 1) Remove any flagged-for-deletion rows
            if (toDelete != null)
            {
                foreach (var id in toDelete)
                {
                    bool existsInZoomMeetingLog = await _context.ZoomMeetingLog.AnyAsync(z => z.ScheduleId == id);
                    var s = await _context.Schedule.FindAsync(id);
                    if (s == null)
                    {
                        continue;
                    }
                    if (s.Status == "Ongoing")
                    {
                        continue;
                    }
                    if (!existsInZoomMeetingLog)
                    {
                        _context.Schedule.Remove(s);
                    }
                    else
                    {
                        s.Status = "Deleted";
                    }
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

            var groupIds = schedules
                .Select(s => s.GroupId)
                .Distinct()
                .ToList();

            var affectedStudentIds = await _context.StudentGroupComposition
                .Where(sgc => groupIds.Contains(sgc.GroupId))
                .Select(sgc => sgc.StudentId)
                .Distinct()
                .ToListAsync();

            foreach (var studentId in affectedStudentIds)
            {
                //_logger.LogInformation("Edited student with studentId: " + studentId);
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }



            //
            // ─── Build “updatedSchedule” JSON for that teacher/date ───
            //
            // 3.1) Convert selectedDate (which is local) → UTC range using your server’s default zone
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            var localStart = new DateTime(
                selectedDate.Year,
                selectedDate.Month,
                selectedDate.Day,
                0, 0, 0,
                DateTimeKind.Unspecified);
            var localEnd = localStart.AddDays(1);        // next midnight
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tzInfo);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tzInfo);

            // 3.2) Instead of “today’s” window, always grab the most recent 15 lessons for this teacher:
            var updatedSchedule = await _context.Schedule
                .Include(s => s.Teacher)
                .Where(s => s.TeacherId == teacherId)
                .OrderByDescending(s => s.DateTime)
                .Take(15)
                .Select(r => new
                {
                    r.Id,
                    r.GroupId,
                    DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                    r.Status,
                    r.Duration,
                    TeacherId = r.TeacherId,
                    TeacherName = r.Teacher != null ? r.Teacher.Name : ""
                })
                .ToListAsync();

            //_logger.LogInformation("Broadcasting ScheduleChanged to teacher_{TeacherId}, count={Count}", teacherId, updatedSchedule.Count);
            //_logger.LogDebug("Payload: {PayloadJson}", JsonSerializer.Serialize(updatedSchedule));


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
        public async Task<IActionResult> CopyLastWeek(Guid teacherId, DateTime selectedDate)
        {
            //_logger.LogInformation("copy last week for {a}, teacher: {b}", selectedDate, teacherId);
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            var windowsZoneId = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            var lastWeekStartLocal = selectedDate.Date.AddDays(-7);
            var lastWeekEndLocal = lastWeekStartLocal.AddDays(1);

            var lastWeekStartUtc = TimeZoneInfo.ConvertTimeToUtc(lastWeekStartLocal, tzInfo);
            var lastWeekEndUtc = TimeZoneInfo.ConvertTimeToUtc(lastWeekEndLocal, tzInfo);

            var lastWeekEntries = await _context.Schedule
                .Where(s =>
                    s.TeacherId == teacherId &&
                    s.DateTime >= lastWeekStartUtc &&
                    s.DateTime < lastWeekEndUtc)
                .ToListAsync();

            var newEntries = new List<Schedule>();
            foreach (var entry in lastWeekEntries)
            {
                //_logger.LogInformation("entry: {a}", entry.Id);
                var newDate = entry.DateTime.AddDays(7);

                var exists = await _context.Schedule.AnyAsync(s =>
                    s.TeacherId == teacherId &&
                    s.DateTime == newDate);

                if (exists)
                    continue;

                var copy = new Schedule
                {
                    Id = Guid.NewGuid(),

                    GroupId = entry.GroupId,
                    TeacherId = entry.TeacherId,

                    StudentChargeAmount = entry.StudentChargeAmount,
                    StudentChargeCurrency = entry.StudentChargeCurrency,
                    TeacherPayAmount = entry.TeacherPayAmount,
                    TeacherPayCurrency = entry.TeacherPayCurrency,

                    DateTime = newDate,
                    Status = "Scheduled",
                    Duration = entry.Duration,
                    LessonAccountingType = "S100T100",
                    Accounted = false,
                    HasReachedMinimumDuration = false
                };

                newEntries.Add(copy);
                _context.Schedule.Add(copy);
            }
            //_logger.LogInformation("new entries count {}", newEntries.Count);
            if (newEntries.Count > 0)
            {
                await _context.SaveChangesAsync();

                var groupIds = newEntries
                    .Select(e => e.GroupId)
                    .Distinct()
                    .ToList();

                var affectedStudentIds = await _context.StudentGroupComposition
                    .Where(sgc => groupIds.Contains(sgc.GroupId))
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
                    .Include(s => s.Teacher)
                    .Where(s => s.TeacherId == teacherId)
                    .OrderByDescending(s => s.DateTime)
                    .Take(15)
                    .Select(r => new
                    {
                        r.Id,
                        r.GroupId,
                        DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                        r.Status,
                        r.Duration,
                        TeacherId = r.TeacherId,
                        TeacherName = r.Teacher != null ? r.Teacher.Name : ""
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

        [HttpPost]
        public async Task<IActionResult> ImportDefault(Guid teacherId, DateTime selectedDate)
        {
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");
            var windowsZoneId   = TZConvert.IanaToWindows(defaultZoneIana);
            var tzInfo          = TimeZoneInfo.FindSystemTimeZoneById(windowsZoneId);

            var weekday = selectedDate.DayOfWeek;

            // Get all default rows for this teacher + weekday
            var defaultEntries = await _context.DefaultSchedule
                .Include(d => d.Assignment!)
                .Where(d => d.Assignment!.TeacherId == teacherId &&
                            d.DayOfWeek == weekday)
                .ToListAsync();
            
            var newEntries = new List<Schedule>();

            foreach (var def in defaultEntries)
            {
                var localDateTime = new DateTime(
                selectedDate.Year,
                selectedDate.Month,
                selectedDate.Day,
                def.DateTime.Hour,
                def.DateTime.Minute,
                def.DateTime.Second,
                DateTimeKind.Unspecified);

            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, tzInfo);

            // Skip if something already exists for that teacher at that exact UTC time
            var exists = await _context.Schedule.AnyAsync(s =>
                s.TeacherId == teacherId &&
                s.DateTime == utcDateTime);

                if (exists)
                    continue;

                if (def.Assignment == null || !def.Assignment.TeacherId.HasValue)
                    continue;

                var copy = new Schedule
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = def.Assignment.Id,
                    GroupId = def.Assignment.GroupId,
                    TeacherId = def.Assignment.TeacherId.Value,

                    StudentChargeAmount = (int)def.Assignment.StudentUnitCost,
                    StudentChargeCurrency = def.Assignment.StudentUnitType,
                    TeacherPayAmount = (int)def.Assignment.TeacherPayForUnit,
                    TeacherPayCurrency = def.Assignment.TeacherPayUnitType,

                    DateTime = utcDateTime,
                    Status = "Scheduled",
                    Duration = def.Duration,
                    LessonAccountingType = "S100T100",
                    Accounted = false,
                    HasReachedMinimumDuration = false
                };

                newEntries.Add(copy);
                _context.Schedule.Add(copy);
            }
            //_logger.LogInformation("new entries count {}", newEntries.Count);
            if (newEntries.Count > 0)
            {
                await _context.SaveChangesAsync();

                var groupIds = newEntries
                    .Select(e => e.GroupId)
                    .Distinct()
                    .ToList();

                var affectedStudentIds = await _context.StudentGroupComposition
                    .Where(sgc => groupIds.Contains(sgc.GroupId))
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
                    .Include(s => s.Teacher)
                    .Where(s => s.TeacherId == teacherId)
                    .OrderByDescending(s => s.DateTime)
                    .Take(15)
                    .Select(r => new
                    {
                        r.Id,
                        r.GroupId,

                        DateTime = r.DateTime.ToUniversalTime().ToString("o"),

                        r.Status,
                        r.Duration,

                        TeacherId = r.TeacherId,
                        TeacherName = r.Teacher != null
                            ? r.Teacher.Name
                            : ""
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

        
        [HttpPost]
        public async Task<IActionResult> SendEmailAboutScheduleChange(Guid teacherId, DateTime selectedDate)
        {
            //_logger.LogInformation("in SendEmailAboutScheduleChange ");
            var teacher = await _context.Teacher.FirstOrDefaultAsync(t => t.Id == teacherId);
            
            if (teacher == null)
            {
                TempData["Error"] = "Failed to send email about schedule change";
                return RedirectToAction(nameof(Manage), new
                {
                    teacherId,
                    date = selectedDate.ToString("yyyy-MM-dd")
                });
            }
            
            var teacherEmail = teacher.Email;
            
            if (teacherEmail == null)
            {
                TempData["Error"] = "Failed to send email about schedule change because teacher doesn't have an email";
                return RedirectToAction(nameof(Manage), new
                {
                    teacherId,
                    date = selectedDate.ToString("yyyy-MM-dd")
                });
            }

            string date = selectedDate.ToString("yyyy-MM-dd");
            var html = $"""
                        <p>Your schedule has been updated for {date}.</p>
                        <p>
                            Please sign in <a href="https://torontofrench.ca/" target="_blank" style="color: #1a73e8;">here</a> to see your schedule.
                        </p>
                        """;
                        
                        string to = teacherEmail;
                        string subject = "Schedule Updated for " + date;
                        await _mailer.SendAsync(
                            to: to,
                            subject: subject,
                            htmlBody: html);

                        _logger.LogInformation("Sent email to: " + to);

                        //await _logHelper.LogMailAsync(to, subject, html);


            

            return RedirectToAction(nameof(Manage), new
            {
                teacherId,
                date = selectedDate.ToString("yyyy-MM-dd")
            });
        }

        public class ScheduleDto
        {
            public Guid Id { get; set; }
            public Guid AssignmentId { get; set; }
            public Guid GroupId { get; set; }
            public Guid TeacherId { get; set; }

            public int StudentChargeAmount { get; set; }
            public string StudentChargeCurrency { get; set; } = "";

            public int TeacherPayAmount { get; set; }
            public string TeacherPayCurrency { get; set; } = "";

            public DateTime SelectedDate { get; set; }
            public DateTime DateTime { get; set; }

            public string Status { get; set; } = "";
            public int Duration { get; set; }

            public string LessonAccountingType { get; set; } = "";

            public bool Accounted { get; set; }
        }


        [HttpPost]
        public async Task<IActionResult> SaveEntry([FromBody] ScheduleDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid payload");

            // 1) Pull in the tracked entity (if it exists)
            var entity = await _context.Schedule
                .FirstOrDefaultAsync(s => s.Id == dto.Id);

            // 2) If it didn't exist, create + add it
            if (entity == null)
            {
                if (dto.AssignmentId == Guid.Empty)
                {
                    return BadRequest("AssignmentId is required when creating a schedule.");
                }

                var assignmentIsValid = await _context.Assignments
                    .AnyAsync(a =>
                        a.Id == dto.AssignmentId &&
                        a.GroupId == dto.GroupId &&
                        a.TeacherId == dto.TeacherId);

                if (!assignmentIsValid)
                {
                    return BadRequest(
                        "The selected assignment does not match the selected group and teacher.");
                }

                entity = new Schedule
                {
                    Id = dto.Id == Guid.Empty
                        ? Guid.NewGuid()
                        : dto.Id,

                    AssignmentId = dto.AssignmentId,

                    GroupId = dto.GroupId,
                    TeacherId = dto.TeacherId,

                    StudentChargeAmount = dto.StudentChargeAmount,
                    StudentChargeCurrency = dto.StudentChargeCurrency,

                    TeacherPayAmount = dto.TeacherPayAmount,
                    TeacherPayCurrency = dto.TeacherPayCurrency,

                    DateTime = dto.DateTime,
                    Status = dto.Status,
                    Duration = dto.Duration,

                    LessonAccountingType = dto.LessonAccountingType,

                    Accounted = false,
                    HasReachedMinimumDuration = false
                };

                _context.Schedule.Add(entity);
            }
            else
            {
                await ProcessAccountingTypeChange(
                    entity.LessonAccountingType,
                    dto.LessonAccountingType,
                    entity.Status,
                    dto.Status,
                    entity);
            }

            entity.GroupId = dto.GroupId;
            entity.TeacherId = dto.TeacherId;

            entity.StudentChargeAmount = dto.StudentChargeAmount;
            entity.StudentChargeCurrency = dto.StudentChargeCurrency;

            entity.TeacherPayAmount = dto.TeacherPayAmount;
            entity.TeacherPayCurrency = dto.TeacherPayCurrency;

            entity.DateTime = dto.DateTime;
            entity.Status = dto.Status;
            entity.Duration = dto.Duration;
            entity.LessonAccountingType = dto.LessonAccountingType;

            // 5) Save and then do any "new" logic
            await _context.SaveChangesAsync();
            await ProcessNewAccountingType(entity.LessonAccountingType, entity);

            var teacherId = entity.TeacherId;

            var studentIds = await _context.StudentGroupComposition
                .Where(sgc => sgc.GroupId == entity.GroupId)
                .Select(sgc => sgc.StudentId)
                .ToListAsync();

            // Broadcast to all affected students
            foreach (var studentId in studentIds)
            {
                await _hub.Clients
                    .Group($"student_{studentId}")
                    .SendAsync("ScheduleChanged");
            }

            // Broadcast to the teacher
            if (teacherId != Guid.Empty)
            {
                await _hub.Clients
                    .Group($"teacher_{teacherId}")
                    .SendAsync("ScheduleChanged");
            }

            //_logger.LogInformation("----------------------------------------------------");

            var notesDate = dto.SelectedDate.Date;

            var rowNotes = await _context.Set<Notes>()
                .Where(n =>
                    studentIds.Contains(n.StudentID) &&
                    (!n.ExpirationDate.HasValue ||
                    n.ExpirationDate.Value >= notesDate))
                .OrderBy(n => n.PriorityLevel ?? 0)
                .ThenByDescending(n => n.CreatedDate)
                .Select(n => new
                {
                    n.Value,
                    n.PriorityLevel
                })
                .ToListAsync();

            var rowStudentIds = studentIds.Distinct().ToList();

            return Json(new
            {
                id = entity.Id,
                groupId = entity.GroupId,
                teacherId = entity.TeacherId,

                studentChargeAmount = entity.StudentChargeAmount,
                studentChargeCurrency = entity.StudentChargeCurrency,

                teacherPayAmount = entity.TeacherPayAmount,
                teacherPayCurrency = entity.TeacherPayCurrency,
                dateTime = entity.DateTime.ToUniversalTime().ToString("o"),
                status = entity.Status,
                duration = entity.Duration,
                lessonAccountingType = entity.LessonAccountingType,

                mainNotes = string.Join(", ",
                    rowNotes
                        .Where(n => !string.IsNullOrWhiteSpace(n.Value))
                        .Select(n => n.Value)),

                notesPriority = rowNotes.Count == 0
                    ? (int?)null
                    : rowNotes.Min(n => n.PriorityLevel ?? 0),

                studentId = rowStudentIds.Count == 1
                    ? (Guid?)rowStudentIds[0]
                    : null
            });
        }



        [HttpPost]
        public async Task<IActionResult> DeleteEntry([FromBody] Guid id)
        {
            // Load the entity with related data before deleting
            var entity = await _context.Schedule
                .Include(s => s.Group!)
                    .ThenInclude(g => g.StudentGroupCompositions)
                .Include(s => s.Teacher)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity != null)
            {
                // Get teacher and student IDs before deleting
                var teacherId = entity.TeacherId;

                var studentIds = entity.Group?.StudentGroupCompositions?
                    .Select(sgc => sgc.StudentId)
                    .ToList() ?? new List<Guid>();

                if (entity.Status == "Ongoing")
                {
                    if (entity != null && entity.Id != Guid.Empty)
                    {
                        await _zoomSvc.EndMeetingsAsync(entity.Id);
                    }
                    return BadRequest("Cannot delete an ongoing class.");
                }

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

                // Broadcast to all affected students
                foreach (var studentId in studentIds)
                {
                    await _hub.Clients
                        .Group($"student_{studentId}")
                        .SendAsync("ScheduleChanged");
                }

                // Broadcast to the teacher if available
                if (teacherId != Guid.Empty)
                {
                    // Get updated schedule for the teacher
                    var updatedSchedule = await _context.Schedule
                        .Include(s => s.Teacher)
                        .Where(s => s.TeacherId == teacherId)
                        .OrderByDescending(s => s.DateTime)
                        .Take(15)
                        .Select(r => new
                        {
                            r.Id,
                            r.GroupId,

                            DateTime = r.DateTime.ToUniversalTime().ToString("o"),

                            r.Status,
                            r.Duration,

                            TeacherId = r.TeacherId,
                            TeacherName = r.Teacher != null
                                ? r.Teacher.Name
                                : ""
                        })
                        .ToListAsync();

                    await _hub.Clients
                        .Group($"teacher_{teacherId}")
                        .SendAsync("ScheduleChanged", updatedSchedule);
                }
            }
            return Ok();
        }
        
        private async Task ProcessAccountingTypeChange(string oldAccountingType, string newAccountingType, string oldStatus, string newStatus, Schedule schedule)
        {
            //_logger.LogInformation("In ProcessAccountingTypeChange method");
            if ((oldAccountingType != newAccountingType && schedule.Accounted) ||
                (oldStatus == "Cancelled" && oldStatus != newStatus && schedule.Accounted))
            {
                _logger.LogInformation("In UNDO If statement--------");
                await _balanceSvc.UndoTransactionAsync(HttpContext.RequestAborted, schedule.Id, oldAccountingType);
                //await _balanceSvc.UpdateStudentBalanceAsync(HttpContext.RequestAborted, schedule.Id);
            }
        }

        private async Task ProcessNewAccountingType(string newValue, Schedule schedule)
        {
            //_logger.LogInformation("In ProcessNewAccountingType method");
            //_logger.LogInformation("in process new, accounted: {1}", schedule.Accounted);
            if (schedule.Accounted || (schedule.Status == "Cancelled" && schedule.Accounted == false))
            {
                //_logger.LogInformation("In ProcessNewAccountingType If statement-----");
                await _balanceSvc.UpdateStudentBalanceAsync(HttpContext.RequestAborted, schedule.Id);
            } 
        }
    }
}