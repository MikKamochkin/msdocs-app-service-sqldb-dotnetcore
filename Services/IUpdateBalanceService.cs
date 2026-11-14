using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface IUpdateBalanceService
    {
        // Sets what students owe for a lesson (its current price when charged, nothing otherwise),
        // applying only the difference from what the transaction log shows was already charged.
        // Also saves any pending changes to the lesson. Returns false if nothing was saved.
        Task<bool> SetLessonChargeAsync(Guid scheduleId, bool charged, CancellationToken cancellationToken);

        Task<Guid?> AddToBalanceAsync(Guid balanceId, float amountPaid,
        string adminNotes, string currency, string payerNotes, string paymentReference, string paymentType);
    }
}