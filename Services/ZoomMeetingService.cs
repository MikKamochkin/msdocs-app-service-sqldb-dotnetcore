using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;


namespace DotNetCoreSqlDb.Services
{
    public class ZoomMeetingService : IZoomMeetingService
    {
        private readonly MyDatabaseContext _context;
        private readonly IZoomApiService _zoomApiService;
        private readonly ILogger<ZoomMeetingService> _logger;

        public ZoomMeetingService(
            MyDatabaseContext context,
            IZoomApiService zoomApiService,
            ILogger<ZoomMeetingService> logger)
        {
            _context = context;
            _zoomApiService = zoomApiService;
            _logger = logger;
        }

        public async Task AssignMeetingsAsync()
        {
            var nowUtc = DateTime.UtcNow;
            var windowStart = nowUtc.AddMinutes(0);
            var windowEnd = nowUtc.AddMinutes(3);

            var upcomingSchedules = await _context.Schedule
                .Where(s => s.DateTime >= windowStart && s.DateTime < windowEnd)
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} upcoming schedules between {Start} and {End}: {@Schedules}",
                upcomingSchedules.Count,
                windowStart,
                windowEnd,
                upcomingSchedules
            );

            foreach (var schedule in upcomingSchedules)
            {
                _logger.LogInformation("upcoming schedules id: " + schedule.Id + " at time: " + schedule.DateTime);
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
                        continue;
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

                    await _context.SaveChangesAsync();
                    await tx2.CommitAsync();

                    _logger.LogInformation(
                        "Updated ZoomMeetings row {ZoomRowId} with join link + timing",
                        freeSlot.Id);
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
            }
        }


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
                    await _zoomApiService.EndMeetingAsync(
                            slot.ZoomId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Zoom end call failed for slot {SlotId}; proceeding with local cleanup",
                        slot.Id);
                }

                // ── clear the slot in DB ────────────────────────────────────────
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

    }
}