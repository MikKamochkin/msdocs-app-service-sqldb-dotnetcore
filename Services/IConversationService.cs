using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Services
{
    public interface IConversationService
    {
        //Task<string> GetAccessTokenAsync();

        Task<Messages?> SendMessageAsync(Guid conversationId, Guid senderId, string? message, IFormFile? file);
    }
}
