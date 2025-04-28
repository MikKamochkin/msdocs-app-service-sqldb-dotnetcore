using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public interface IWhatsAppService
    {
        Task<IEnumerable<WhatsAppVoiceMessages>> GetAllMessagesAsync();
        Task<(bool Valid, bool Exists, string? Error)> ValidateContactAsync(string phone);
        Task SendAsync(Guid messageId, string phone);
        Task<string?> GetStatusAsync(Guid messageId);
    }
}