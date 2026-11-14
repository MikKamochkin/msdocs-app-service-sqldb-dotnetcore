using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCoreSqlDb.Helpers
{
    public static class ScheduleRules
    {
        public const string ChargedLessonDeleteMessage =
            "This lesson can't be deleted while students are charged for it. Set its student charge to 0 first.";

        private const float Epsilon = 0.005f;

        // A lesson can't be deleted while students are charged for it: taken/cancelled lessons need their
        // charge set to 0 first (which refunds it), and no lesson may leave a net charge in the transaction log.
        public static bool IsBlockedByCharge(Schedule schedule, bool hasOutstandingCharge) =>
            hasOutstandingCharge ||
            ((schedule.Status == "Taken" || schedule.Status == "Cancelled") &&
             schedule.StudentChargeAmount != 0);

        public static async Task<bool> IsDeleteBlockedAsync(MyDatabaseContext context, Schedule schedule)
        {
            var outstanding = await GetSchedulesWithOutstandingChargeAsync(context, new[] { schedule.Id });
            return IsBlockedByCharge(schedule, outstanding.Contains(schedule.Id));
        }

        // Lessons where some student still has a non-zero net charge (per currency) in the transaction log
        public static async Task<HashSet<Guid>> GetSchedulesWithOutstandingChargeAsync(
            MyDatabaseContext context, IEnumerable<Guid> scheduleIds)
        {
            var ids = scheduleIds.Distinct().ToList();
            if (ids.Count == 0)
                return new HashSet<Guid>();

            var nets = await context.StudentBalanceTransactionLog
                .Where(l => l.ScheduleId != null && ids.Contains(l.ScheduleId.Value))
                .GroupBy(l => new { l.ScheduleId, l.StudentId, l.Currency })
                .Select(g => new { g.Key.ScheduleId, Net = g.Sum(l => l.TransactionAmount ?? 0) })
                .ToListAsync();

            return nets
                .Where(n => Math.Abs(n.Net) >= Epsilon)
                .Select(n => n.ScheduleId!.Value)
                .ToHashSet();
        }
    }
}
