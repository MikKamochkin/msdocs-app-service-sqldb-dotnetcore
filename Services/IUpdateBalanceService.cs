using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface IUpdateBalanceService
    {
        Task UpdateStudentBalanceAsync(CancellationToken cancellationToken, Guid scheduleId);

        Task UndoTransactionAsync(CancellationToken cancellationToken, Guid scheduleId, string originalAccountingType);

        Task<Guid?> AddToBalanceAsync(Guid balanceId, float unitsToAdd, float amountPaid,
        string adminNotes, string currency, string payerNotes, string paymentReference, string paymentType);
    }
}