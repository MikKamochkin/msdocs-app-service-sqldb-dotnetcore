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


namespace DotNetCoreSqlDb.Services
{
    public class ZoomMeetingService : IZoomMeetingService
    {
        private readonly MyDatabaseContext _context;
        private readonly IZoomApiService _zoomApiService;
        private readonly ILogger<ZoomMeetingService> _logger;
        private readonly IHubContext<ZoomMeetingHub> _hub;

        public ZoomMeetingService(
            MyDatabaseContext context,
            IZoomApiService zoomApiService,
            ILogger<ZoomMeetingService> logger,
            IHubContext<ZoomMeetingHub> hub)
        {
            _context = context;
            _zoomApiService = zoomApiService;
            _logger = logger;
            _hub = hub;

        }

        //Creates a meeting based on scheduleId param
        public async Task AssignMeetingsAsync(Guid scheduleId)
        {
            var zoomMeetings = await _context.ZoomMeetings
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId ||
                                           z.ScheduleId2 == scheduleId);

            if (zoomMeetings == null)
            {
                var schedule = await _context.Schedule
                    .FirstOrDefaultAsync(s => s.Id == scheduleId);

                ZoomMeetings? freeSlot = null;
                bool useSecond = false;
                try
                {
                    // 1) Reserve a free Zoom slot
                    using var tx1 = await _context.Database
                        .BeginTransactionAsync(IsolationLevel.Serializable);

                    freeSlot = await _context.ZoomMeetings
                        .Where(m => !m.IsBusy || !m.IsBusy2)
                        .FirstOrDefaultAsync();

                    if (freeSlot == null)
                    {
                        _logger.LogWarning("No free Zoom users for schedule {ScheduleId}", schedule.Id);
                        await tx1.RollbackAsync();
                        return;
                    }

                    useSecond = freeSlot.IsBusy; // first slot taken?
                    if (useSecond)
                    {
                        freeSlot.IsBusy2 = true;
                        freeSlot.ScheduleId2 = schedule.Id;
                    }
                    else
                    {
                        freeSlot.IsBusy = true;
                        freeSlot.ScheduleId = schedule.Id;
                    }
                    await _context.SaveChangesAsync();
                    await tx1.CommitAsync();

                    // 2) Create the instant Zoom meeting
                    var hostIdentifier = string.IsNullOrWhiteSpace(freeSlot.ZoomId)
                                            ? freeSlot.Email
                                            : freeSlot.ZoomId;

                    var result = await _zoomApiService.CreateInstantMeetingAsync(
                                        hostIdentifier,
                                        topic: $"Lesson – {schedule.Id}");

                    _logger.LogInformation(
                        "Instant meeting {MeetingId} created for schedule {ScheduleId}",
                        result.Id, schedule.Id);

                    // 3) Persist Zoom response + timing in your DB
                    using var tx2 = await _context.Database
                                                    .BeginTransactionAsync(IsolationLevel.Serializable);

                    if (useSecond)
                    {
                        freeSlot.MeetingId2 = result.Id;
                        freeSlot.UUid2 = result.Uuid;
                        freeSlot.JoinUrl2 = result.JoinUrl;
                        freeSlot.MeetingPassword2 = result.Passcode;
                        freeSlot.StartTime2 = DateTime.UtcNow;       // actual create time
                        freeSlot.Duration2 = schedule.Duration;       // from your Schedule
                    }
                    else
                    {
                        freeSlot.MeetingId = result.Id;
                        freeSlot.UUid = result.Uuid;
                        freeSlot.JoinUrl = result.JoinUrl;
                        freeSlot.MeetingPassword = result.Passcode;
                        freeSlot.StartTime = DateTime.UtcNow;         // actual create time
                        freeSlot.Duration = schedule.Duration;         // from your Schedule
                    }
                    schedule.Status = "Ongoing";

                    await _context.SaveChangesAsync();
                    await tx2.CommitAsync();

                    _logger.LogInformation(
                        "Updated ZoomMeetings row {ZoomRowId} with join link + timing",
                        freeSlot.Id);

                    await _hub.Clients.Group(scheduleId.ToString()).SendAsync("MeetingCreated", result.JoinUrl);
                }
                catch (Exception ex)
                {
                    // if we reserved a slot and something failed, mark it free again
                    if (freeSlot != null)
                    {
                        if (useSecond)
                        {
                            freeSlot.IsBusy2 = false;
                            freeSlot.ScheduleId2 = null;
                        }
                        else
                        {
                            freeSlot.IsBusy = false;
                            freeSlot.ScheduleId = null;
                        }
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogError(
                        ex,
                        "Failed to assign or persist Zoom meeting for schedule {ScheduleId}",
                        schedule.Id);
                }

                _logger.LogInformation("From meetingservice; found schedule entry with id: {id}", scheduleId);
            }
            else
            {
                _logger.LogInformation("A meeting with scheduleId {scheduleId} already exists, didn't do anything", scheduleId);
            }
        }

        public async Task EndMeetingsAsync(Guid scheduleId)
        {

            var zoomRow = await _context.ZoomMeetings
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId ||
                                           z.ScheduleId2 == scheduleId);

            if (zoomRow != null)
            {
                bool second = zoomRow.ScheduleId2 == scheduleId;
                string? uuid = second ? zoomRow.UUid2 : zoomRow.UUid;
                try
                {
                    if (!string.IsNullOrWhiteSpace(uuid))
                    {
                        await _zoomApiService.EndZoomMeetingAsync(uuid);
                    }
                    else
                    {
                        await _zoomApiService.EndMeetingAsync(zoomRow.ZoomId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Zoom end call failed for slot {SlotId}; proceeding with local cleanup",
                        zoomRow.Id);
                }

                // ── clear the slot in DB ────────────────────────────────────────
                try
                {
                    using var tx = await _context.Database
                                                    .BeginTransactionAsync(IsolationLevel.Serializable);

                    if (second)
                    {
                        zoomRow.IsBusy2 = false;
                        zoomRow.ScheduleId2 = null;
                        zoomRow.JoinUrl2 = null;
                        zoomRow.MeetingId2 = null;
                        zoomRow.MeetingPassword2 = null;
                        zoomRow.StartTime2 = null;
                        zoomRow.Duration2 = null;
                        zoomRow.UUid2 = null;
                    }
                    else
                    {
                        zoomRow.IsBusy = false;
                        zoomRow.ScheduleId = null;
                        zoomRow.JoinUrl = null;
                        zoomRow.MeetingId = null;
                        zoomRow.MeetingPassword = null;
                        zoomRow.StartTime = null;
                        zoomRow.Duration = null;
                        zoomRow.UUid = null;
                    }

                    var schedule = await _context.Schedule
                                        .FirstOrDefaultAsync(s => s.Id == scheduleId);
                    if (schedule != null)
                    {
                        schedule.Status = "Taken";
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogInformation("Cleared Zoom slot record {SlotId}", zoomRow.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Database cleanup failed for Zoom slot {SlotId}", zoomRow.Id);
                }
            }

            _logger.LogError("Could not find a ZoomMeeting row with scheduleId {scheduleID}", scheduleId);
        }

        //Automatic meating cleanup that runs from timer and ends all meetings that ended >= 15 mins ago
        public async Task CleanupMeetingsAsync()
        {
            const int bufferAfterMeetingEnd = 15; // min

            var nowUtc = DateTime.UtcNow;
            var busySlots = await _context.ZoomMeetings
                    .Where(m => (m.IsBusy && m.StartTime.HasValue && m.Duration.HasValue) ||
                                (m.IsBusy2 && m.StartTime2.HasValue && m.Duration2.HasValue))
                    .ToListAsync();

            foreach (var slot in busySlots)
            {
                if (slot.IsBusy && slot.StartTime.HasValue && slot.Duration.HasValue)
                {
                    var endMoment = slot.StartTime.Value
                                              .AddMinutes(slot.Duration.Value)
                                              .AddMinutes(bufferAfterMeetingEnd);

                    _logger.LogInformation("endMoment is {End} for slot {SlotId}", endMoment, slot.Id);

                    if (nowUtc >= endMoment)
                    {
                        await CleanupSlotAsync(slot, second: false);
                    }
                }

                if (slot.IsBusy2 && slot.StartTime2.HasValue && slot.Duration2.HasValue)
                {
                    var endMoment2 = slot.StartTime2.Value
                                               .AddMinutes(slot.Duration2.Value)
                                               .AddMinutes(bufferAfterMeetingEnd);

                    _logger.LogInformation("endMoment2 is {End} for slot {SlotId}", endMoment2, slot.Id);

                    if (nowUtc >= endMoment2)
                    {
                        await CleanupSlotAsync(slot, second: true);
                    }
                }
            }

            async Task CleanupSlotAsync(ZoomMeetings slot, bool second)
            {
                try
                {
                    string? uuid = second ? slot.UUid2 : slot.UUid;
                    if (!string.IsNullOrWhiteSpace(uuid))
                    {
                        await _zoomApiService.EndZoomMeetingAsync(uuid);
                    }
                    else
                    {
                        await _zoomApiService.EndMeetingAsync(slot.ZoomId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Zoom end call failed for slot {SlotId}; proceeding with local cleanup",
                        slot.Id);
                }

                try
                {
                    using var tx = await _context.Database
                        .BeginTransactionAsync(IsolationLevel.Serializable);

                    if (second)
                    {
                        slot.IsBusy2 = false;
                        slot.ScheduleId2 = null;
                        slot.JoinUrl2 = null;
                        slot.MeetingId2 = null;
                        slot.MeetingPassword2 = null;
                        slot.StartTime2 = null;
                        slot.Duration2 = null;
                        slot.UUid2 = null;
                    }
                    else
                    {
                        slot.IsBusy = false;
                        slot.ScheduleId = null;
                        slot.JoinUrl = null;
                        slot.MeetingId = null;
                        slot.MeetingPassword = null;
                        slot.StartTime = null;
                        slot.Duration = null;
                        slot.UUid = null;
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogInformation("Cleared Zoom slot record {SlotId}", slot.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Database cleanup failed for Zoom slot {SlotId}", slot.Id);
                }
            }
        }

    }
}