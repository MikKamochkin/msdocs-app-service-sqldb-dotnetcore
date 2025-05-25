using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface IZoomApiService
{
    Task<string> GetAccessTokenAsync();

    Task<ZoomParticipantViewModel> ListLiveMeetingParticipantsAsync(string meetingId);

    Task<ZoomCreateMeetingResult> CreateInstantMeetingAsync(string hostIdOrEmail,
                                                            string topic = "Lesson");

    /// <summary>
    /// End a meeting by number, with optional UUID and host-id fall-back.
    /// </summary>
    Task EndMeetingAsync(string zoomId);

    //Task<string?> LookupUuidAsync(string meetingNumber);
}
}
