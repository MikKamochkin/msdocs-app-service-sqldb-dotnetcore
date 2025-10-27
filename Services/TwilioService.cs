using System;
using System.Collections.Generic;
using System.Linq;
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

namespace DotNetCoreSqlDb.Services
{
    /*public class TwilioService : ITwilioService
    {
        //private readonly MyDatabaseContext _context;
        private readonly ILogger<TwilioService> _logger;
        private readonly string _accountSid;
        private readonly string _authToken;

        public TwilioService(
            //MyDatabaseContext context,
            IConfiguration config,
            ILogger<TwilioService> logger
            )
        {
            //_context = context;
            _logger = logger;
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
