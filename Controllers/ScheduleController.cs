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

            // map each assignment to its StudentUnitDuration
            var durationMap = await _context.Assignments
                .Where(a => teacherId.HasValue && a.TeacherId == teacherId)
                .ToDictionaryAsync(a => a.Id.ToString(), a => a.StudentUnitDuration);

            ViewBag.DurationMapJson = JsonSerializer.Serialize(durationMap);


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

            // 6) Groups (i.e. students) for that teacher
            List<SelectListItem> groupItems = new();
            if (teacherId.HasValue)
            {
                var assignments = await _context.Assignments
                    .Where(a => a.TeacherId == teacherId && a.Group!.IsActive && a.IsActive == true)
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
                    //.Where(a => a.IsActive)
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
                    .Include(s => s.Assignment!)
                        .ThenInclude(a => a.Group!)
                            .ThenInclude(g => g.StudentGroupCompositions)
                                .ThenInclude(sgc => sgc.Student)
                    .Where(s =>
                        s.Assignment!.TeacherId == teacherId &&
                        s.DateTime >= startUtc &&
                        s.DateTime < endUtc &&
                        s.Status != "Deleted"
                    )
                    .OrderBy(s => s.DateTime)
                    .ToListAsync();
            }

            var flat = existing.Select(r => new ScheduleRowDto
            {
                Id = r.Id,
                AssignmentId = r.AssignmentId,
                DateTime = r.DateTime.ToUniversalTime().ToString("o"),
                Status = r.Status,
                Duration = r.Duration,
                Accounted = r.Accounted,
                LessonAccountingType = r.LessonAccountingType,
                GroupName = r.Assignment?.Group?.Name ?? string.Empty,

                // Sum of all notes of all students in the group for THIS schedule row
                MainNotes = string.Join(" ",
                    r.Assignment?.Group?.StudentGroupCompositions?
                        .Select(sgc => sgc.Student?.MainNotes)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                    ?? Enumerable.Empty<string>())
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
            public string DateTime { get; set; } = "";
            public string Status { get; set; } = "";
            public int Duration { get; set; }
            public string LessonAccountingType { get; set; } = "";
            public string MainNotes { get; set;} = "";
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

            var assignmentIds = schedules.Select(s => s.AssignmentId).Distinct().ToList();

            // Query StudentGroupCompositions linked to those assignments via GroupId
            var affectedStudentIds = await _context.Assignments
                .Where(a => assignmentIds.Contains(a.Id))
                .Include(a => a.Group!)
                    .ThenInclude(g => g.StudentGroupCompositions)
                .SelectMany(a => a.Group!.StudentGroupCompositions)
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
                    .Include(a => a.Group!)
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
                s.Assignment!.TeacherId == teacherId &&
                s.DateTime == utcDateTime);

                if (exists)
                    continue;

                var copy = new Schedule
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = def.AssignmentId,
                    DateTime = utcDateTime,
                    Status = "Scheduled",
                    Duration = def.Duration,
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
                    .Include(a => a.Group!)
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
                .Include(s => s.Assignment!)
                .ThenInclude(a => a.Group!)
                .ThenInclude(g => g.StudentGroupCompositions)
                .FirstOrDefaultAsync(s => s.Id == dto.Id);

            // 2) If it didn't exist, create + add it
            if (entity == null)
            {
                entity = new Schedule {
                    Id                 = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                    AssignmentId       = dto.AssignmentId,
                    DateTime           = dto.DateTime,
                    Status             = dto.Status,
                    Duration           = dto.Duration,
                    LessonAccountingType = dto.LessonAccountingType,
                    Accounted          = false
                };
                _context.Schedule.Add(entity);
            }
            else
            {
                // 3) If it *did* exist, do your "undo" logic first
                await ProcessAccountingTypeChange(
                    entity.LessonAccountingType,
                    dto.LessonAccountingType,
                    entity.Status,
                    dto.Status,
                    entity);
            }

            // 4) Update the entity's properties
            entity.AssignmentId       = dto.AssignmentId;
            entity.DateTime           = dto.DateTime;
            entity.Status             = dto.Status;
            entity.Duration           = dto.Duration;
            entity.LessonAccountingType = dto.LessonAccountingType;
            //entity.Accounted          = dto.Accounted;

            // 5) Save and then do any "new" logic
            await _context.SaveChangesAsync();
            await ProcessNewAccountingType(entity.LessonAccountingType, entity);

            // Get the teacher ID from the assignment
            var teacherId = await _context.Assignments
                .Where(a => a.Id == entity.AssignmentId)
                .Select(a => a.TeacherId)
                .FirstOrDefaultAsync();

            // Get all student IDs in the group
            var studentIds = await _context.Assignments
                .Where(a => a.Id == entity.AssignmentId)
                .SelectMany(a => a.Group!.StudentGroupCompositions)
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

            return Json(new {
                entity.Id,
                entity.AssignmentId,
                DateTime = entity.DateTime.ToUniversalTime().ToString("o"),
                entity.Status,
                entity.Duration,
                entity.LessonAccountingType
            });
        }



        [HttpPost]
        public async Task<IActionResult> DeleteEntry([FromBody] Guid id)
        {
            // Load the entity with related data before deleting
            var entity = await _context.Schedule
                .Include(s => s.Assignment!)
                    .ThenInclude(a => a.Group!)
                        .ThenInclude(g => g.StudentGroupCompositions)
                .Include(s => s.Assignment!)
                    .ThenInclude(a => a.Teacher)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity != null)
            {
                // Get teacher and student IDs before deleting
                var teacherId = entity.Assignment?.TeacherId ?? Guid.Empty;
                var studentIds = entity.Assignment?.Group?.StudentGroupCompositions?
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