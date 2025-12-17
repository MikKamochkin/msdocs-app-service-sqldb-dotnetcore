using System.Threading.Tasks;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public interface IEmailInboxReader
    {
        Task CheckInboxAsync();
    }
}
