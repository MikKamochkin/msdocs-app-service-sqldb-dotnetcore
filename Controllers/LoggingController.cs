using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DotNetCoreSqlDb.Services;

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
            var TwoWeeksAgo = DateTime.UtcNow.Date.AddDays(-14);
            var queue = await _context.InteracPaymentsQueue
                .OrderByDescending(d => d.AddedToQueueTime)
                .Where(d => d.AddedToQueueTime >= TwoWeeksAgo)
                .ToListAsync();

            return View(queue);
        }

        [HttpPost]
        public async Task<IActionResult> ReRunEmailProcessing()
        {
            using var scope = _scopeFactory.CreateScope();
            var emailReader  = scope.ServiceProvider.GetRequiredService<IEmailInboxReader>();

            await emailReader.CheckInboxAsync();

            return RedirectToAction("InteracPaymentsQueue");
        }
    }
}