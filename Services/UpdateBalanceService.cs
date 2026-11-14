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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using DotNetCoreSqlDb.Services;
using DotNetCoreSqlDb.Helpers;
using Microsoft.AspNetCore.Mvc;



namespace DotNetCoreSqlDb.Services
{
    public class UpdateBalanceService : IUpdateBalanceService
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<UpdateBalanceService> _logger;
        private readonly LogHelper _logHelper;

        public UpdateBalanceService(
            MyDatabaseContext context,
            ILogger<UpdateBalanceService> logger,
            LogHelper logHelper)
        {
            _context = context;
            _logger = logger;
            _logHelper = logHelper;
        }

        public const string LessonChargeType = "LessonCharge";
        public const string LessonAdjustmentType = "LessonAdjustment";
        public const string LessonRefundType = "LessonRefund";
        public const string LessonChargeBackfillType = "LessonChargeBackfill";

        private const float Epsilon = 0.005f;

        // Brings the students' balances in line with what the lesson should cost them, applying only the
        // difference from what the transaction log says they have already been charged for this lesson.
        //   charged = true  -> each charged student owes StudentChargeAmount in StudentChargeCurrency
        //   charged = false -> nobody owes anything for this lesson (full refund)
        //
        // Pending changes to the lesson (tracked by the shared DbContext, including a newly added lesson)
        // are saved in the same transaction, so the lesson edit and the balance change succeed or fail together.
        // Safe to repeat: re-running with the same lesson state writes nothing.
        // Returns false (with all pending changes discarded) if nothing could be saved.
        public async Task<bool> SetLessonChargeAsync(Guid scheduleId, bool charged, CancellationToken cancellationToken)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);

                await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

                if (!await AcquireBalanceLockAsync(connection, tx, cancellationToken))
                {
                    await tx.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return false;
                }

                // Tracked instance (with this request's pending edits), or a newly added lesson
                var lesson = await _context.Schedule.FindAsync(new object[] { scheduleId }, cancellationToken);
                if (lesson == null)
                {
                    _logger.LogWarning("SetLessonChargeAsync: schedule {ScheduleId} not found.", scheduleId);
                    await tx.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return false;
                }

                string chargeCurrency = NormalizeCurrency(lesson.StudentChargeCurrency);
                if (charged && !StudentBalanceHelper.IsSupportedCurrency(chargeCurrency))
                {
                    _logger.LogWarning(
                        "Schedule {ScheduleId} has unsupported charge currency '{Currency}'; balance not updated.",
                        scheduleId, lesson.StudentChargeCurrency);
                    await tx.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return false;
                }

                // The lesson as committed, read under the lock (null for a lesson that isn't saved yet)
                var stored = await _context.Schedule
                    .AsNoTracking()
                    .Where(s => s.Id == scheduleId)
                    .Select(s => new { s.Accounted, s.StudentChargeAmount, s.StudentChargeCurrency, s.DateTime })
                    .SingleOrDefaultAsync(cancellationToken);
                bool wasCharged = stored?.Accounted == true;

                var logRows = await _context.StudentBalanceTransactionLog
                    .Where(l => l.ScheduleId == scheduleId && l.Currency != null && l.TransactionAmount != null)
                    .Select(l => new LessonLogRow(l.StudentId, l.Currency!, l.TransactionAmount!.Value))
                    .ToListAsync(cancellationToken);

                var roster = await _context.StudentGroupComposition
                    .Where(sgc => sgc.GroupId == lesson.GroupId && sgc.UseMyBalance)
                    .Select(sgc => sgc.StudentId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var studentIds = roster.Concat(logRows.Select(r => r.StudentId)).Distinct().ToList();
                var balances = await _context.StudentBalance
                    .Where(b => studentIds.Contains(b.StudentId))
                    .ToDictionaryAsync(b => b.StudentId, cancellationToken);

                // Lessons charged before charges were logged: record the charge they would have had
                // (the lesson's price before this change, for the current group), without touching balances.
                if (wasCharged && logRows.Count == 0 && stored!.StudentChargeAmount != 0)
                {
                    string oldCurrency = NormalizeCurrency(stored.StudentChargeCurrency);
                    if (StudentBalanceHelper.IsSupportedCurrency(oldCurrency))
                    {
                        foreach (var studentId in roster.Where(balances.ContainsKey))
                        {
                            _context.StudentBalanceTransactionLog.Add(new StudentBalanceTransactionLog
                            {
                                StudentId = studentId,
                                ScheduleId = scheduleId,
                                DateTime = stored.DateTime,
                                Currency = oldCurrency,
                                TransactionAmount = -stored.StudentChargeAmount,
                                PaymentType = LessonChargeBackfillType,
                                AdminNotes = "Original lesson charge was not logged; recorded at the lesson's price before it was changed. Balance not affected."
                            });
                            logRows.Add(new LessonLogRow(studentId, oldCurrency, -stored.StudentChargeAmount));
                        }
                    }
                }

                // Who owes for this lesson: a lesson that was already charged keeps the students it was
                // charged to; a fresh charge goes to the group's current members who use their balance.
                var chargedStudents = new HashSet<Guid>();
                if (charged)
                {
                    var source = wasCharged && logRows.Count > 0
                        ? logRows.Select(r => r.StudentId)
                        : roster;
                    chargedStudents.UnionWith(source.Where(balances.ContainsKey));
                }

                // Net already charged per student and currency (log amounts are negative for charges)
                var net = logRows
                    .GroupBy(r => (r.StudentId, Currency: NormalizeCurrency(r.Currency)))
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

                var keys = net.Keys
                    .Concat(chargedStudents.Select(id => (StudentId: id, Currency: chargeCurrency)))
                    .Distinct()
                    .ToList();

                foreach (var key in keys)
                {
                    float target = chargedStudents.Contains(key.StudentId) && key.Currency == chargeCurrency
                        ? -lesson.StudentChargeAmount
                        : 0f;
                    float current = net.TryGetValue(key, out var n) ? n : 0f;
                    float delta = (float)Math.Round(target - current, 2, MidpointRounding.AwayFromZero);

                    if (Math.Abs(delta) < Epsilon)
                        continue;

                    if (!StudentBalanceHelper.IsSupportedCurrency(key.Currency))
                    {
                        _logger.LogWarning(
                            "Schedule {ScheduleId}: logged charge in unsupported currency '{Currency}' for student {StudentId} left as is.",
                            scheduleId, key.Currency, key.StudentId);
                        continue;
                    }

                    if (!balances.TryGetValue(key.StudentId, out var sb))
                    {
                        _logger.LogWarning(
                            "Schedule {ScheduleId}: student {StudentId} has no balance row; charge not adjusted.",
                            scheduleId, key.StudentId);
                        continue;
                    }

                    float newBalance = StudentBalanceHelper.AdjustBalance(sb, key.Currency, delta);

                    _context.StudentBalanceTransactionLog.Add(new StudentBalanceTransactionLog
                    {
                        StudentId = key.StudentId,
                        ScheduleId = scheduleId,
                        DateTime = DateTime.Now,
                        Currency = key.Currency,
                        TransactionAmount = delta,
                        CurrentBalance = newBalance,
                        PaymentType = Math.Abs(current) < Epsilon ? LessonChargeType
                                    : Math.Abs(target) < Epsilon ? LessonRefundType
                                    : LessonAdjustmentType
                    });
                }

                lesson.Accounted = charged;

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SetLessonChargeAsync was canceled for schedule {ScheduleId}; rolled back.", scheduleId);
                _context.ChangeTracker.Clear();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SetLessonChargeAsync for schedule {ScheduleId}", scheduleId);
                _context.ChangeTracker.Clear();
                return false;
            }
        }

        private sealed record LessonLogRow(Guid StudentId, string Currency, float Amount);

        private static string NormalizeCurrency(string? currency) =>
            (currency ?? "").Trim().ToUpperInvariant();

        // Serializes all balance updates; the lock is held until the transaction ends.
        private async Task<bool> AcquireBalanceLockAsync(
            System.Data.Common.DbConnection connection,
            IDbContextTransaction tx,
            CancellationToken cancellationToken)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = "sp_getapplock";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 10; // seconds

            cmd.Parameters.Add(new SqlParameter("@Resource", "UpdateStudentBalance"));
            cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
            cmd.Parameters.Add(new SqlParameter("@LockOwner", "Transaction"));
            cmd.Parameters.Add(new SqlParameter("@LockTimeout", 10000)); // ms

            var ret = new SqlParameter("@return_value", SqlDbType.Int)
            {
                Direction = ParameterDirection.ReturnValue
            };
            cmd.Parameters.Add(ret);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var lockResult = (int)ret.Value;
            if (lockResult < 0)
            {
                _logger.LogWarning(
                    "Could not acquire application lock (code {LockResult}); balance not updated.",
                    lockResult);
                return false;
            }
            return true;
        }

        public async Task<Guid?> AddToBalanceAsync(
            Guid balanceId,
            float amountPaid,
            string adminNotes,
            string currency,
            string payerNotes,
            string paymentReference,
            string paymentType)
        {
            if (!StudentBalanceHelper.IsSupportedCurrency(currency)) return null;

            var balance = await _context.StudentBalance.FindAsync(balanceId);
            if (balance == null) return null;

            float newBalance = StudentBalanceHelper.AdjustBalance(balance, currency, amountPaid);
            await _context.SaveChangesAsync();

            var student = await _context.Student.FindAsync(balance.StudentId);
            if (student == null) return null;

            var transactionLogId =
                await _logHelper.LogStudentBalanceAsync(
                    student.ID,
                    currency,
                    paymentType,
                    paymentReference,
                    payerNotes,
                    adminNotes,
                    amountPaid,
                    amountPaid,
                    newBalance);

            return transactionLogId;
        }

    }
}