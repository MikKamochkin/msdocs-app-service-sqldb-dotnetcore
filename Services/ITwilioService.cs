using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface ITwilioService
    {
        //Task CallNumberForLessonStart(Guid scheduleId);
        Task TextNumberForLessonReminder(Guid scheduleId);
        Task RemindStudents();
    }
}
