using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;               // for SignOutAsync(...)
using Microsoft.AspNetCore.Authentication.Cookies;    // <-- Adjust if your DbContext lives in a different namespace
using DotNetCoreSqlDb.Models; 


namespace DotNetCoreSqlDb.Helpers
{
    public class LogHelper
    {
        private readonly MyDatabaseContext _context;

        public LogHelper(MyDatabaseContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task LogMailAsync(
            string to,
            string subject,
            string body)
        {

            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("Recipient (to) is required.", nameof(to));
            if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));
            if (body == null) body = string.Empty;

            var log = new MailLog
            {
                To = to,
                Subject = subject,
                Body = body,
                Time = DateTime.Now
            };
            _context.MailLog.Add(log);

            await _context.SaveChangesAsync();
        }

    }

}
