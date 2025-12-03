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

        public async Task LogSignInAsync(
            Guid userId,
            string attemptedUsername,
            string ipAddress,
            bool wasSuccessful)
        {
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.ID == userId);

            if (user != null)
            {
                var log = new SignInLog
                {
                    UserId = userId,
                    UserName = attemptedUsername,
                    WasSuccessful = wasSuccessful,
                    IpAddress = ipAddress,
                    Time = DateTime.Now
                };
                _context.SignInLog.Add(log);

                await _context.SaveChangesAsync();
            }
        }

        public async Task LogSignInAsync(
            string attemptedUsername,
            string ipAddress,
            bool wasSuccessful)
        {
            var log = new SignInLog
            {
                UserId = null,
                UserName = attemptedUsername,
                WasSuccessful = wasSuccessful,
                IpAddress = ipAddress,
                Time = DateTime.Now
            };
            _context.SignInLog.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogStudentBalanceAsync(
            Guid studentId,
            Guid assignmentId,
            string currency,
            string paymentType,
            string paymentReference,
            string payerNotes,
            string adminNotes,
            float transactionAmount,
            float amountPaid
            )
        {
            var log = new StudentBalanceTransactionLog
            {
                StudentId = studentId,
                AssignmentId = assignmentId,
                DateTime = DateTime.Now,
                Currency = currency,
                PaymentType = paymentType,
                PaymentReference = paymentReference,
                PayerNotes = payerNotes,
                AdminNotes = adminNotes,
                TransactionAmount = transactionAmount,
                AmountPaid = amountPaid
            };
            _context.StudentBalanceTransactionLog.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogStudentBalanceAsync(
            Guid studentId,
            Guid assignmentId,
            Guid scheduleId,
            float transactionAmount,
            float currentBalance)
        {
            var log = new StudentBalanceTransactionLog
            {
                StudentId = studentId,
                AssignmentId = assignmentId,
                DateTime = DateTime.Now,
                ScheduleId = scheduleId,
                CurrentBalance = currentBalance,
                TransactionAmount = transactionAmount
            };
            _context.StudentBalanceTransactionLog.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogZoomMeetingStartAsync(
            Guid scheduleId,
            Guid zoomMeetingId)
        {
            var zoomMeeting = await _context.ZoomMeetings.FindAsync(zoomMeetingId);
            if (zoomMeeting != null)
            {
                var log = new ZoomMeetingLog
                {
                    DateTime = DateTime.Now,
                    ScheduleId = scheduleId,
                    ZoomMeetingId = zoomMeetingId,
                    ZoomId = zoomMeeting.ZoomId,
                    Email = zoomMeeting.Email,
                    JoinUrl = zoomMeeting.JoinUrl,
                    StartTime = zoomMeeting.StartTime,
                    Duration = zoomMeeting.Duration
                };
                _context.ZoomMeetingLog.Add(log);
                await _context.SaveChangesAsync();
            }  
        }

        public async Task LogZoomMeetingEndAsync(Guid scheduleId)
        {
            var zoomMeeting = await _context.ZoomMeetings
                .Where(zm => zm.ScheduleId == scheduleId)
                .FirstOrDefaultAsync();
                
            var existingLog = await _context.ZoomMeetingLog
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId);

            if (existingLog != null)
            {
                existingLog.EndTime = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }


        public async Task LogTwilioLessonReminderAsync(
            string body,
            string number,
            Guid scheduleId)
        {
            var log = new TwilioLessonRemindersLog
            {
                Body = body,
                Number = number,
                DateTime = DateTime.UtcNow,
                ScheduleId = scheduleId
            };
            _context.TwilioLessonRemindersLog.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
