using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using DotNetCoreSqlDb.Helpers;

namespace DotNetCoreSqlDb.Services
{
    public class NotesService : INotesService
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<TimedHostedService> _logger;
        public NotesService(
            MyDatabaseContext context,
            ILogger<TimedHostedService> logger)
        {
            _context = context;
            _logger = logger;

        }

        //Automatic note cleanup that runs from timer and removes all notes that have expired
        public async Task CleanupNotesAsync()
        {
            var now = DateTime.UtcNow;
            var expiredNotes = await _context.Note
                .Where(n => n.ExpirationDate <= now)
                .ToListAsync();

            if (expiredNotes.Any())
            {
                //_logger.LogInformation($"Cleaning up {expiredNotes.Count} expired notes.");
                _context.Note.RemoveRange(expiredNotes);
                await _context.SaveChangesAsync();
            }
        }
    
    }
}