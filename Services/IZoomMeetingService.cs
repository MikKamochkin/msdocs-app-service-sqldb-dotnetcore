using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface IZoomMeetingService
    {
        Task AssignMeetingsAsync(Guid id);
        Task EndMeetingsAsync(Guid id);
        Task CleanupMeetingsAsync();
        Task MarkLessonAsSufficient();
    }
}