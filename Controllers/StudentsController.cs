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

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize]
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
                .FirstOrDefaultAsync(s => s.ID.ToString() == userId);

            if (student == null)
                return NotFound();

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
                .OrderBy(s => s.DateTime)
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
            ViewBag.TimeZones = TimeZoneMapping.GetTimeZones();

            // 6) Pass the raw schedule entries into the Razor view
            return View(scheduleEntries);
        }

        public async Task<IActionResult> Calendar()
        {return View();}
    }
}
