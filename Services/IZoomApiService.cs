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
    /// End all live meetings for a Zoom user.
    /// </summary>
    Task EndMeetingAsync(string zoomId);

    /// <summary>
    /// End a specific meeting by its UUID.
    /// </summary>
    Task EndZoomMeetingAsync(string meetingUuid);

    //Task<string?> LookupUuidAsync(string meetingNumber);
}
}
