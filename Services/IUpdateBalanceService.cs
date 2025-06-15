using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface IUpdateBalanceService
    {
        Task UpdateStudentBalanceAsync(CancellationToken cancellationToken, Guid scheduleId);

        Task UndoTransactionAsync(CancellationToken cancellationToken, Guid scheduleId, string originalAccountingType);
    }
}