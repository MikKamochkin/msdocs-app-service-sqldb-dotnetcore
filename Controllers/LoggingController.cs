using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotNetCoreSqlDb.Services;
using System.Text.RegularExpressions;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin")]
    public class LoggingController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<LoggingController> _logger;
        private readonly IEmailSender _mailer;
        private readonly ITwilioService _twilioSvc;
        private readonly IServiceScopeFactory _scopeFactory;


        public LoggingController(
            MyDatabaseContext context,
            ILogger<LoggingController> logger,
            IEmailSender mailer,
            ITwilioService twilioSvc,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _logger = logger;
            _mailer = mailer;
            _twilioSvc = twilioSvc;
            _scopeFactory = scopeFactory;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }
        // GET: Mail
        public async Task<IActionResult> Mail(string sortOrder)
        {
            var mails = await _context.MailLog
                .OrderByDescending(d => d.Time)
                .ToListAsync();

            //return View(mails);
            return NotFound();
        }

        public async Task<IActionResult> SignIn(string sortOrder)
        {
            var dayAgo = DateTime.UtcNow.AddHours(-24);
            var signIns = await _context.SignInLog
                .OrderByDescending(d => d.Time)
                .Where(d => d.Time >= dayAgo)
                .ToListAsync();

            return View(signIns);
        }

        public async Task<IActionResult> TwilioLessonReminders()
        {
            var dayAgo = DateTime.UtcNow.AddHours(-24);
            var reminders = await _context.TwilioLessonRemindersLog
                .OrderByDescending(d => d.DateTime)
                .Where(d => d.DateTime >= dayAgo)
                .ToListAsync();

            return View(reminders);
        }

        public async Task<IActionResult> ZoomMeetingsLog()
        {
            var dayAgo = DateTime.UtcNow.AddHours(-24);
            var meetings = await _context.ZoomMeetingLog
                .OrderByDescending(d => d.DateTime)
                .Where(d => d.DateTime >= dayAgo)
                .ToListAsync();

            return View(meetings);
        }

        public async Task<IActionResult> EmailPaymentsLog()
        {
            var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
            var payments = await _context.EmailPaymentsLog
                .Include(e => e.StudentBalanceTransactionLog)
                .OrderByDescending(d => d.DateTime)
                .Where(d => d.DateTime >= weekAgo)
                .ToListAsync();

            return View(payments);
        }

        public async Task<IActionResult> InteracPaymentsQueue()
        {
            var TwoWeeksAgo = DateTime.UtcNow.Date.AddMonths(-1);
            var queue = await _context.InteracPaymentsQueue
                .OrderByDescending(d => d.AddedToQueueTime)
                .Where(d => d.AddedToQueueTime >= TwoWeeksAgo)
                .ToListAsync();

            return View(queue);
        }

        //TO DO:Lowk move ts out of logging into a service or smtg
        [HttpPost]
        public async Task<IActionResult> ReRunEmailProcessing()
        {
            using var scope = _scopeFactory.CreateScope();
            var emailReader = scope.ServiceProvider.GetRequiredService<IEmailInboxReader>();

            await emailReader.CheckInboxAsync();

            return RedirectToAction("InteracPaymentsQueue");
        }

        [HttpGet]
        public async Task<IActionResult> GetBalanceOptions(Guid paymentId)
        {
            var q = await _context.InteracPaymentsQueue
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (q == null) return NotFound("Queue item not found.");

            var subject = q.Subject ?? "";
            var sentFromMatch = Regex.Match(
                subject,
                @"from\s+(.+?)\s+and it has been",
                RegexOptions.Multiline);

            var sentFrom = sentFromMatch.Success
                ? sentFromMatch.Groups[1].Value.Trim()
                : "";

            if (string.IsNullOrWhiteSpace(sentFrom))
                return BadRequest("Could not parse payer from subject.");

            var payers = await _context.Payer
                .AsNoTracking()
                .Include(p => p.Student)
                .Where(p => EF.Functions.Collate(p.Name, "Latin1_General_CS_AS") == sentFrom)
                .ToListAsync();

            if (payers == null) return NotFound("Payer not found.");

            var students = payers
                .Where(p => p.Student != null)
                .Select(p => p.Student)
                .ToList();

            if (students == null) return NotFound("Payer has no linked students");

            var studentIds = students.Select(s => s!.ID).ToList();

            var options = await _context.StudentBalance
                .AsNoTracking()
                .Include(sb => sb.Assignment)
                    .ThenInclude(a => a.Teacher)
                .Include(sb => sb.Student)
                .Where(sb => studentIds.Contains(sb.StudentId))
                .Where(sb => sb.Assignment.IsActive)
                .Select(sb => new
                {
                    studentId = sb.StudentId,
                    studentName = sb.Student.Name,
                    id = sb.Id,
                    label = sb.Assignment.Teacher!.Name + " - " + sb.Balance.ToString("0.##") + " units"
                })
                .ToListAsync();

            return Ok(options);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyQueuedPayment(Guid paymentId, Guid balanceId)
        {
            using var scope = _scopeFactory.CreateScope();
            var emailReader = scope.ServiceProvider.GetRequiredService<IEmailInboxReader>();

            var q = await _context.InteracPaymentsQueue
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (q == null) return NotFound("Queue item not found.");

            var subject = q.Subject;

            var body = q.Body;

            if (body == null || subject == null)
            {
                return BadRequest();
            }

            await emailReader.HandleEtransferEmailWithSelectedBalance(paymentId, body, subject, balanceId);
            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _context.Student
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new { id = s.ID, name = s.Name })
                .ToListAsync();

            return Ok(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayerToStudent(Guid paymentId, Guid studentId)
        {
            try
            {
                var q = await _context.InteracPaymentsQueue
                    .FirstOrDefaultAsync(x => x.Id == paymentId);

                if (q == null)
                    return NotFound(new { success = false, message = "Queue item not found." });

                var subject = q.Subject ?? "";

                var sentFromMatch = Regex.Match(
                    subject,
                    @"from\s+(.+?)\s+and it has been",
                    RegexOptions.Multiline);

                var payerName = sentFromMatch.Success
                    ? sentFromMatch.Groups[1].Value.Trim()
                    : "";

                if (string.IsNullOrWhiteSpace(payerName))
                    return BadRequest(new { success = false, message = "Could not parse payer from subject." });

                // student exists?
                var student = await _context.Student
                    .Include(s => s.Payers)
                    .FirstOrDefaultAsync(s => s.ID == studentId);

                if (student == null)
                    return NotFound(new { success = false, message = "Student not found." });

                // prevent duplicate (same student + same payer name)
                var alreadyLinked = await _context.Payer.AnyAsync(p =>
                    p.StudentId == studentId &&
                    EF.Functions.Collate(p.Name, "Latin1_General_CS_AS") == payerName);

                if (!alreadyLinked)
                {
                    _context.Payer.Add(new Payer
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        Name = payerName
                    });

                    // optional
                    q.ReasonForFailure = "Payer linked manually";

                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, payerName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddPayerToStudent failed. paymentId={PaymentId} studentId={StudentId}", paymentId, studentId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }




    }
}