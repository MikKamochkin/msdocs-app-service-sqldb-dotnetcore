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
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId);

            if (zoomMeetings == null)
            {
                var schedule = await _context.Schedule
                    .FirstOrDefaultAsync(s => s.Id == scheduleId);

                ZoomMeetings? freeSlot = null;
                try
                {
                    // 1) Reserve a free Zoom slot
                    using var tx1 = await _context.Database
                        .BeginTransactionAsync(IsolationLevel.Serializable);

                    freeSlot = await _context.ZoomMeetings
                        .Where(m => !m.IsBusy)
                        .FirstOrDefaultAsync();

                    if (freeSlot == null)
                    {
                        _logger.LogWarning("No free Zoom users for schedule {ScheduleId}", schedule.Id);
                        await tx1.RollbackAsync();
                        return;
                    }

                        freeSlot.IsBusy = true;
                        freeSlot.ScheduleId = schedule.Id;
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

                        freeSlot.MeetingId = result.Id;
                        freeSlot.UUid = result.Uuid;
                        freeSlot.JoinUrl = result.JoinUrl;
                        freeSlot.MeetingPassword = result.Passcode;
                        freeSlot.StartTime = DateTime.UtcNow;           // actual create time
                        freeSlot.Duration = schedule.Duration;         // from your Schedule
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
                            freeSlot.IsBusy = false;
                            freeSlot.ScheduleId = null;
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
                .FirstOrDefaultAsync(z => z.ScheduleId == scheduleId);
            
            if (zoomRow != null)
            {
                try
                {
                    /*await _zoomApiService.EndMeetingAsync(
                            zoomRow.ZoomId);*/
                    await _zoomApiService.EndZoomMeetingAsync(zoomRow.MeetingId);
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

                    zoomRow.IsBusy = false;
                    zoomRow.ScheduleId = null;
                    zoomRow.JoinUrl = null;
                    zoomRow.MeetingId = null;
                    zoomRow.MeetingPassword = null;
                    zoomRow.StartTime = null;
                    zoomRow.Duration = null;
                    zoomRow.UUid = null;
                    zoomRow.FirstMutualPresenceTime = null;

                    var schedule = await _context.Schedule
                                        .FirstOrDefaultAsync(s => s.Id == scheduleId);
                    if (schedule != null)
                    {
                        //_logger.LogInformation("Changed status for schedule with id: {d}", schedule.Id);
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
                    .Where(m => m.IsBusy &&
                                m.StartTime.HasValue &&
                                m.Duration.HasValue)
                    .ToListAsync();

            foreach (var slot in busySlots)
            {
                var endMoment = slot.StartTime!.Value
                                              .AddMinutes(slot.Duration!.Value)
                                              .AddMinutes(bufferAfterMeetingEnd);

                _logger.LogInformation("endMoment is {End} for slot {SlotId}", endMoment, slot.Id);

                if (nowUtc < endMoment) continue;


                try
                {
                    /*await _zoomApiService.EndMeetingAsync(
                            slot.ZoomId);*/
                    await _zoomApiService.EndZoomMeetingAsync(slot.MeetingId);
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

                    slot.IsBusy = false;
                    slot.ScheduleId = null;
                    slot.JoinUrl = null;
                    slot.MeetingId = null;
                    slot.MeetingPassword = null;
                    slot.StartTime = null;
                    slot.Duration = null;
                    slot.UUid = null;

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

        public async Task MarkLessonAsSufficient()
        {

            const float sufficientPercentageOfLesson = 0.5f;
            var activeLessons = await _context.ZoomMeetings
                //.Include(z => z.Schedule)
                .Where(z => z.IsBusy && z.UUid != null && z.UUid != "")
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var lesson in activeLessons)
            {
                var count = await GetLiveParticipantCountAsync(lesson.UUid!);

                if (count >= 2 && !lesson.FirstMutualPresenceTime.HasValue)
                {
                    lesson.FirstMutualPresenceTime = now;
                }
            }

            await _context.SaveChangesAsync();

            var lessonsWithFirstMutualPresenceTime = await _context.ZoomMeetings
                .Include(z => z.Schedule)
                .Where(z => z.FirstMutualPresenceTime != null)
                .ToListAsync();


            foreach (var lesson in lessonsWithFirstMutualPresenceTime)
            {
                var lessonDuration = lesson.Schedule!.Duration;
                var timeSinceFirstMutualPresenceTime = now - lesson.FirstMutualPresenceTime;
                var count = await GetLiveParticipantCountAsync(lesson.UUid!);
                
                bool lessonSufficient = timeSinceFirstMutualPresenceTime?.TotalMinutes >= lessonDuration * sufficientPercentageOfLesson && count >= 2;

                if (lessonSufficient)
                {
                    lesson.Schedule.HasReachedMinimumDuration = true;
                }
            }

            await _context.SaveChangesAsync();
        }
        
        private async Task<int> GetLiveParticipantCountAsync(string uuid)
        {
            try
            {
                if (!string.IsNullOrEmpty(uuid))
                {
                    var vm = await _zoomApiService.ListLiveMeetingParticipantsAsync(uuid);
                    // If you want to store remaining rate-limit somewhere, you can expose it here as well.
                    return vm?.Participants?.Count ?? 0;
                }
                else
                {
                    return 0;
                }

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching live participants for meeting UUID {Uuid}", uuid);
                return 0;
            }
        }
    }
}