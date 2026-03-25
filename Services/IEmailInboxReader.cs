using System.Threading.Tasks;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public interface IEmailInboxReader
    {
        Task CheckInboxAsync();
        Task ProcessEmailsInQueue();
        Task HandleEtransferEmail(Guid queueItemId, string body, string subject);
        Task HandleEtransferEmailWithSelectedBalance(Guid paymentId, string body, string subject, Guid balanceId);
    }
}
