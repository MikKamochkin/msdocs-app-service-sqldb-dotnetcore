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
    public class MailController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<MailController> _logger;
        private readonly IEmailSender _mailer;


        public MailController(
            MyDatabaseContext context,
            ILogger<MailController> logger,
            IEmailSender mailer)
        {
            _context = context;
            _logger = logger;
            _mailer = mailer;
        }



        // GET: Mail
        public async Task<IActionResult> Index(string sortOrder)
        {
            var mails = await _context.MailLog
                .OrderByDescending(d => d.Time)
                .ToListAsync();

            return View(mails);
        }
    }
}