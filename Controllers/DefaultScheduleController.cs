using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Rendering;
using TimeZoneConverter;

namespace DotNetCoreSqlDb.Controllers
{

    // GET: /DefaultSchedule
    [Authorize(Roles = "support, admin, assistant")]
    public class DefaultScheduleController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<DefaultScheduleController> _logger;

        public DefaultScheduleController(MyDatabaseContext context, ILogger<DefaultScheduleController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /DefaultSchedule
        public async Task<IActionResult> Index()
        {
            var vm = new DefaultSchedulePageVm();

            // 1) Teachers
            vm.Teachers = await _context.Teacher
                .Select(t => new TeacherVm
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .OrderBy(t => t.Name)
                .ToListAsync();

            // 2) Teacher -> assignments (students) map
            var teacherAssignments = await _context.Assignments
                .Include(a => a.Group)
                .Where(a => a.IsActive == true &&
                            a.Group!.IsActive &&
                            a.TeacherId != null)
                .ToListAsync();

            vm.TeacherAssignments = teacherAssignments
                .GroupBy(a => a.TeacherId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => new AssignmentOptionVm
                    {
                        Id = a.Id,
                        GroupName = a.Group!.Name
                    })
                    .OrderBy(x => x.GroupName)
                    .ToList()
                );

            // 3) Time zones (same as Manage)
            var timeZones = TimeZoneMapping.GetTimeZones();
            var defaultZoneIana = TZConvert.WindowsToIana("Eastern Standard Time");

            foreach (var tz in timeZones)
            {
                tz.Selected = tz.Value == defaultZoneIana;
            }

            ViewBag.TimeZones = timeZones;
            ViewBag.DefaultZoneIana = defaultZoneIana;

            // 4) Dropdowns for the modal (same idea as Schedule/Manage)
            ViewBag.Times = Enumerable.Range(0, 24 * 12)
                .Select(i => TimeSpan.FromMinutes(i * 5))
                .Select(ts => new SelectListItem
                {
                    Value = ts.ToString(@"hh\:mm"),
                    Text = ts.ToString(@"hh\:mm")
                })
                .ToList();

            ViewBag.Statuses = DropdownOptions.ScheduleStatusTypes
                .Select(item => new SelectListItem
                {
                    Text = item.Text,
                    Value = item.Value,
                    Selected = item.Value == "Scheduled"
                });

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

            // 5) JSON for JS (teacher -> assignments map)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            ViewBag.TeacherAssignmentsJson =
                JsonSerializer.Serialize(vm.TeacherAssignments, jsonOptions);

            return View(vm);
        }

        // POST: /DefaultSchedule/UpdateCell
        // (your existing UpdateCell as-is)
        // POST: /DefaultSchedule/UpdateCell
        [HttpPost]
        public async Task<IActionResult> UpdateCell([FromBody] UpdateDefaultCellDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid payload");

            if (!TimeSpan.TryParse(dto.Time, out var timeOfDay))
                return BadRequest("Invalid time format");

            // Find existing default entry for this teacher + day + time
            var existing = await _context.DefaultSchedule
                .Include(d => d.Assignment)
                .FirstOrDefaultAsync(d =>
                    d.DayOfWeek == dto.DayOfWeek &&
                    d.Assignment!.TeacherId == dto.TeacherId &&
                    d.DateTime.TimeOfDay == timeOfDay);

            // Clear cell
            if (dto.AssignmentId == null || dto.AssignmentId == Guid.Empty)
            {
                if (existing != null)
                {
                    _context.DefaultSchedule.Remove(existing);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    Id = (Guid?)null,
                    AssignmentId = (Guid?)null,
                    StudentName = ""
                });
            }

            // Validate assignment belongs to teacher
            var assignment = await _context.Assignments
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a =>
                    a.Id == dto.AssignmentId &&
                    a.TeacherId == dto.TeacherId);

            if (assignment == null)
                return BadRequest("Assignment not found for this teacher");

            // Template time stored as arbitrary date + time
            var templateDate = new DateTime(
                2000, 1, 1,
                timeOfDay.Hours,
                timeOfDay.Minutes,
                0,
                DateTimeKind.Unspecified);

            // Fallbacks
            var duration = dto.Duration > 0
                ? dto.Duration
                : (int)assignment.StudentUnitDuration;

            var status = string.IsNullOrWhiteSpace(dto.Status)
                ? "Scheduled"
                : dto.Status;

            var accountingType = string.IsNullOrWhiteSpace(dto.LessonAccountingType)
                ? "S100T100"
                : dto.LessonAccountingType;

            DefaultSchedule entity;
            if (existing == null)
            {
                entity = new DefaultSchedule
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = assignment.Id,
                    DayOfWeek = dto.DayOfWeek,
                    DateTime = templateDate,
                    Status = status,
                    Duration = duration,
                    LessonAccountingType = accountingType,
                    Accounted = false
                };
                _context.DefaultSchedule.Add(entity);
            }
            else
            {
                entity = existing;
                entity.AssignmentId = assignment.Id;
                entity.DateTime = templateDate;
                entity.DayOfWeek = dto.DayOfWeek;
                entity.Status = status;
                entity.Duration = duration;
                entity.LessonAccountingType = accountingType;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                entity.Id,
                entity.AssignmentId,
                StudentName = assignment.Group!.Name
            });
        }



        // GET: /DefaultSchedule/Events?teacherId=...
        [HttpGet]
        public async Task<IActionResult> Events(Guid teacherId)
        {
            if (teacherId == Guid.Empty)
                return Json(Array.Empty<object>());

            var defaults = await _context.DefaultSchedule
                .Include(d => d.Assignment!)
                    .ThenInclude(a => a.Group)
                .Include(d => d.Assignment!)
                    .ThenInclude(a => a.Teacher)
                .Where(d => d.Assignment!.TeacherId == teacherId)
                .ToListAsync();

            // Fixed reference Monday (abstract default week)
            var referenceMonday = new DateTime(2000, 1, 3); // 2000-01-03 is a Monday

            var events = new List<object>();

            foreach (var d in defaults)
            {
                // Map DayOfWeek to offset from Monday
                int dowOffset = ((int)d.DayOfWeek + 6) % 7; // Monday=1->0, Sunday=0->6
                var dateForThisDow = referenceMonday.AddDays(dowOffset);

                var startLocal = new DateTime(
                    dateForThisDow.Year,
                    dateForThisDow.Month,
                    dateForThisDow.Day,
                    d.DateTime.Hour,
                    d.DateTime.Minute,
                    d.DateTime.Second,
                    DateTimeKind.Unspecified);

                var endLocal = startLocal.AddMinutes(d.Duration);

                events.Add(new
                {
                    id = d.Id,
                    assignmentId = d.AssignmentId,
                    title = d.Assignment!.Group!.Name,
                    start = startLocal.ToString("o"),
                    end = endLocal.ToString("o"),
                    status = d.Status,
                    accountingType = d.LessonAccountingType,
                    duration = d.Duration
                });
            }

            return Json(events);
        }

    }



    public class UpdateDefaultCellDto
    {
        public Guid TeacherId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string Time { get; set; } = "";   // "HH:mm"
        public Guid? AssignmentId { get; set; }

        public string Status { get; set; } = "Scheduled";
        public int Duration { get; set; }
        public string LessonAccountingType { get; set; } = "S100T100";
    }

}
