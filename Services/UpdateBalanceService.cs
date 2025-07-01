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

        //Creates a meeting based on scheduleId param
        public async Task UpdateStudentBalanceAsync(CancellationToken cancellationToken, Guid scheduleId)
        {
            try
            {
                // Ensure the underlying connection is open
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);

                // Begin an EF Core transaction (holds our applock until Commit/Rollback)
                await using var tx = await _context.Database
                    .BeginTransactionAsync(cancellationToken);

                // 1) Acquire the application lock with a 10,000 ms timeout
                using (var cmd = connection.CreateCommand())
                {
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
                    // If shutdown requested mid‐command, this will throw
                    cancellationToken.ThrowIfCancellationRequested();

                    var lockResult = (int)ret.Value;
                    if (lockResult < 0)
                    {
                        _logger.LogWarning(
                            "Could not acquire application lock (code {LockResult}); aborting balance update.",
                            lockResult);
                        await tx.RollbackAsync(cancellationToken);
                        return;
                    }
                }

                var lesson = await _context.Schedule
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Group)
                            .ThenInclude(g => g.StudentGroupCompositions)
                    .SingleOrDefaultAsync(s => s.Id == scheduleId);

                if (lesson != null)
                {
                    var a = lesson.Assignment;
                    if (a!.StudentUnitDuration > 0)
                    {
                        float studentCostMutliplier = 1;
                        string lessonType = lesson.LessonAccountingType;
                        switch (lessonType)
                        {
                            case "S100T100":
                                studentCostMutliplier = 1;
                                break;
                            case "S50T100":
                                studentCostMutliplier = 0.5f;
                                break;
                            case "S0T0":
                                studentCostMutliplier = 0;
                                break;
                            case "S0T100":
                                studentCostMutliplier = 0;
                                break;
                            default:
                                studentCostMutliplier = 1;
                                break;
                        }

                        float spent = (float)(lesson.Duration / a.StudentUnitDuration) * studentCostMutliplier;
                        foreach (var sgc in a.Group.StudentGroupCompositions.Where(sgc => sgc.UseMyBalance == true))
                        {
                            Guid studentId = sgc.StudentId;

                            // 1) fetch (or create) the StudentBalance row for this student & assignment
                            var sb = await _context.StudentBalance
                                .SingleOrDefaultAsync(b => b.StudentId == studentId &&
                                                        b.AssignmentId == a.Id);
                            // 2) decrease the balance
                            if (sb != null)
                            {
                                sb.Balance -= spent;
                                await _logHelper.LogStudentBalanceAsync(studentId, a.Id, scheduleId, spent, sb.Balance);
                            }
                            
                        }
                        lesson.Accounted = true;
                    }  
                }

                // 3) Persist and release lock
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("UpdateStudentBalanceAsync was canceled. Rolling back any pending work.");
                // disposal of tx will auto-rollback if not committed
            }
            catch (Exception ex)
            {
                // Catch everything else so we don't leave things hung
                _logger.LogError(ex, "Unexpected error in UpdateStudentBalanceAsync");
            }
        }

        public async Task UndoTransactionAsync(CancellationToken cancellationToken, Guid scheduleId, string originalAccountingType)
        {
            try
            {
                // 1) Ensure the underlying connection is open
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);

                // 2) Begin an EF Core transaction (this holds our applock until Commit/Rollback)
                await using var tx = await _context.Database
                    .BeginTransactionAsync(cancellationToken);

                // 3) Acquire the application lock with a 10,000 ms timeout
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx.GetDbTransaction();
                    cmd.CommandText = "sp_getapplock";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 10; // seconds

                    cmd.Parameters.Add(new SqlParameter("@Resource", "UpdateStudentBalance"));
                    cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
                    cmd.Parameters.Add(new SqlParameter("@LockOwner", "Transaction"));
                    cmd.Parameters.Add(new SqlParameter("@LockTimeout", 10000));

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
                            "Could not acquire application lock (code {LockResult}); aborting UndoTransaction.",
                            lockResult);
                        await tx.RollbackAsync(cancellationToken);
                        return;
                    }
                }

                var lesson = await _context.Schedule
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Group)
                            .ThenInclude(g => g.StudentGroupCompositions)
                    .SingleOrDefaultAsync(s => s.Id == scheduleId);
                    
                if (lesson != null)
                {

                    if (lesson.Accounted == true)
                    {
                        var a = lesson.Assignment;
                        if (a!.StudentUnitDuration > 0)
                        {
                            float studentCostMultiplier = 1;
                            string lessonType = originalAccountingType;
                            switch (lessonType)
                            {
                                case "S100T100":
                                    studentCostMultiplier = 1;
                                    break;
                                case "S50T100":
                                    studentCostMultiplier = 0.5f;
                                    break;
                                case "S0T0":
                                    studentCostMultiplier = 0;
                                    break;
                                case "S0T100":
                                    studentCostMultiplier = 0;
                                    break;
                                default:
                                    studentCostMultiplier = 1;
                                    break;
                            }

                            float spent = (float)(lesson.Duration / a.StudentUnitDuration) * studentCostMultiplier;
                            foreach (var sgc in a.Group.StudentGroupCompositions.Where(sgc => sgc.UseMyBalance == true))
                            {
                                Guid studentId = sgc.StudentId;

                                var sb = await _context.StudentBalance
                                    .SingleOrDefaultAsync(b => b.StudentId == studentId && b.AssignmentId == a.Id);

                                if (sb != null)
                                {
                                    sb.Balance += spent;
                                    await _logHelper.LogStudentBalanceAsync(studentId, a.Id, scheduleId, -spent, sb.Balance);
                                }
                            }
                            lesson.Accounted = true;
                        }
                    }
                } 

                // 6) Persist and release lock
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "UndoTransactionAsync was canceled. Rolling back any pending work.");
                // disposal of 'tx' will auto-rollback if not committed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in UndoTransactionAsync");
            }
        }

    }
}