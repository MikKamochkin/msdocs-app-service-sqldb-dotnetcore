using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support, admin")]
    public class ZoomMeetingsController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IZoomApiService   _zoom;
        private readonly ILogger<ZoomMeetingsController> _logger;

        public ZoomMeetingsController(
            MyDatabaseContext context,
            IZoomApiService   zoom,
            ILogger<ZoomMeetingsController> logger)
        {
            _context = context;
            _zoom    = zoom;
            _logger  = logger;
        }

        public async Task<IActionResult> Index()
        {
            var rows = await _context.ZoomMeetings
                .Include(z => z.Schedule)
                    .ThenInclude(s => s!.Assignment!)
                        .ThenInclude(a => a.Group)
                .Select(z => new
                {
                    z.Id,
                    z.Email,
                    z.JoinUrl,
                    z.MeetingId,
                    z.MeetingPassword,
                    z.IsBusy,
                    z.StartTime,
                    z.Duration,
                    z.ScheduleId,
                    z.UUid,
                    GroupName = z.Schedule!.Assignment!.Group!.Name
                })
                .OrderBy(z => z.IsBusy)
                .ToListAsync();

            // 2) for each row, call into ZoomApiService
            var enriched = await Task.WhenAll(rows.Select(async r =>
            {
                if (string.IsNullOrWhiteSpace(r.UUid))
                {
                    return new
                    {
                        r.Id,
                        r.Email,
                        r.JoinUrl,
                        r.MeetingId,
                        r.MeetingPassword,
                        r.IsBusy,
                        r.StartTime,
                        r.Duration,
                        r.ScheduleId,
                        r.GroupName,
                        r.UUid,
                        Participants = new List<ZoomParticipant>()
                    };
                }

                var vm = await SafeParticipantsAsync(r.UUid!);
                return new
                {
                    r.Id,
                    r.Email,
                    r.JoinUrl,
                    r.MeetingId,
                    r.MeetingPassword,
                    r.IsBusy,
                    r.StartTime,
                    r.Duration,
                    r.ScheduleId,
                    r.GroupName,
                    UUid = (string?)r.UUid,
                    Participants = vm.Participants
                };
            }));

            ViewBag.ZoomRows = enriched;
            return View();
        }

        private async Task<ZoomParticipantViewModel> SafeParticipantsAsync(string meetingId)
        {
            try
            {
                var result = await _zoom.ListLiveMeetingParticipantsAsync(meetingId);

                // store latest remaining calls
                ViewBag.RateLimitRemaining = result.RateLimitRemaining;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching participants for {M}", meetingId);

                // indicate failure
                ViewBag.RateLimitRemaining = -1;
                return new ZoomParticipantViewModel();
            }
        }
    }
}
