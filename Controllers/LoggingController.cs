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


        public LoggingController(
            MyDatabaseContext context,
            ILogger<LoggingController> logger,
            IEmailSender mailer)
        {
            _context = context;
            _logger = logger;
            _mailer = mailer;
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
    }
}