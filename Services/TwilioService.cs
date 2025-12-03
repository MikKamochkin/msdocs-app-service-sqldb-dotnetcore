using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Data;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using DotNetCoreSqlDb.Services;
using DotNetCoreSqlDb.Helpers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using TimeZoneConverter;


namespace DotNetCoreSqlDb.Services
{
    public class TwilioService : ITwilioService
    {
        private readonly ILogger<TwilioService> _logger;
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly MyDatabaseContext _context;
        private readonly LogHelper _logHelper;

        public TwilioService(
            MyDatabaseContext context,
            IConfiguration config,
            ILogger<TwilioService> logger,
            LogHelper logHelper
            )
        {
            _context = context;
            _logger = logger;
            _logHelper = logHelper;
            _accountSid = config["TwilioAccountSid"] ?? throw new InvalidOperationException("_accountSid not configured");
            _authToken = config["TwilioAuthToken"] ?? throw new InvalidOperationException("_authToken not configured");
        }


        public async Task RemindStudents()
        {
            var now = DateTime.UtcNow;
            var inFiftyNineMins = now.AddMinutes(59);
            var inSixtyThreeMins = now.AddMinutes(63);
            
            //lessons that are in an hour
            /*
            lesson is at 2pm est
            it is 12:59
            12:59 + 59 = 1:58
            12:59 + 63 = 2:02
            */
            var upcomingLessons = await _context.Schedule
                .Where(s => s.DateTime > now && s.DateTime < inSixtyThreeMins && s.DateTime > inFiftyNineMins)
                .ToListAsync();

            foreach(var lesson in upcomingLessons)
            {
                await TextNumberForLessonReminder(lesson.Id); 
            }
        }
        public async Task TextNumberForLessonReminder(Guid scheduleId)
        {
            var studentContacts = await _context.Schedule
                .AsNoTracking()
                .Where(s => s.Id == scheduleId)
                .SelectMany(s => s.Assignment.Group.StudentGroupCompositions)
                .Select(sgc => sgc.Student)
                .Distinct()
                .Select(student => new
                {
                    StudentId = student.ID,
                    StudentName = student.Name,
                    PhoneNumber = student.Contacts
                        .Where(c => c.Type == "Phone" && c.Invitation)
                        .Select(c => c.Value)
                        .FirstOrDefault()
                })
                .Where(sc => sc.PhoneNumber != null) // Only include students with phone numbers
                .ToListAsync();
            
            var schedule = await _context.Schedule
                .AsNoTracking()
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Teacher)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
            {
                _logger.LogInformation("sched is null???");
                return;
            }

            var lessonTimeUtc = schedule.DateTime;
            var easternZone = TZConvert.GetTimeZoneInfo("Eastern Standard Time"); 
            var lessonTimeEst = TimeZoneInfo.ConvertTimeFromUtc(lessonTimeUtc, easternZone);
            var teacherName = schedule.Assignment?.Teacher?.Name ?? "your teacher";
            
            foreach (var studentContact in studentContacts)
            {
                if (studentContact.PhoneNumber != null)
                {
                    var normal = NormalizePhone(studentContact.PhoneNumber);

                    if (!IsValidPhone(normal))
                    {
                        continue;
                    }

                    var finalPhone = normal;

                    //temp regex to only send to canadian numbers:
                    bool isCanadianNumber = Regex.IsMatch(finalPhone, @"^\+1");

                    if (isCanadianNumber)
                    {
                        string lessonReminderBody = $"Hello {studentContact.StudentName}!\nThis is a reminder that you have a lesson scheduled at {lessonTimeEst} with {teacherName}";
                        await textNumberWithTwilio(lessonReminderBody, finalPhone);
                        await _logHelper.LogTwilioLessonReminderAsync(lessonReminderBody, finalPhone, scheduleId);
                    }
                }
            }
        }

        public async Task textNumberWithTwilio(string textBody, string number)
        {
            try
            {
                TwilioClient.Init(_accountSid, _authToken);
                var message = await MessageResource.CreateAsync(
                    from: new PhoneNumber("+16475840531"),
                    to: new PhoneNumber(number),
                    body: textBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send message to {number}. Error: {ex.Message}");
            }
        }

        string NormalizePhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            raw = raw.Trim();

            var cleaned = Regex.Replace(raw, @"[^0-9+]", "");

            if (!cleaned.StartsWith("+"))
                cleaned = "+" + cleaned;

            return cleaned;
        }

        bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }
            return Regex.IsMatch(phone, @"^\+[1-9]\d{1,14}$");
        }

    }
        

        /*public async Task CallNumberForLessonStart(Guid scheduleId)
        {
            //var schedule = await _context.Schedule.FindAsync(scheduleId);
            var assignment = await _context.Assignment
                .Where(a => a.ScheduleId == scheduleId);

            var sgc = await _context.StudentGroupComposition
                .Where(sgc => sgc.Assignment == assignment);

            int studentsInGroup = sgc.Count();

            _logger.LogInformation(studentsInGroup + " many students in this group: " + scheduleId);
            //var sgc = await _context.StudentGroupComposition

            if (studentsInGroup == 1)
            {
                TwilioClient.Init(accountSid, authToken);

                var call = await CallResource.CreateAsync(
                    url: new Uri("http://demo.twilio.com/docs/voice.xml"),
                    to: new Twilio.Types.PhoneNumber("+14168042614"),
                    from: new Twilio.Types.PhoneNumber("+18005550199"));

            }

        }

    }*/
        
}
