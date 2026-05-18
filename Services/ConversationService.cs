using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using DotNetCoreSqlDb.Data;                 
using DotNetCoreSqlDb.Services.Models;      
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Helpers;
using Microsoft.EntityFrameworkCore;


namespace DotNetCoreSqlDb.Services
{
    public class ConversationService : IConversationService
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<ConversationService> _logger;
        private readonly LogHelper _logHelper;
        private readonly IBlobService _blobService;

        public ConversationService(
            MyDatabaseContext context,
            ILogger<ConversationService> logger,
            LogHelper logHelper,
            IBlobService blobService)
        {
            _context = context;
            _logger = logger;
            _logHelper = logHelper;
            _blobService = blobService;
        }

        public async Task<Messages?> SendMessageAsync(Guid conversationId, Guid senderId, string? message, IFormFile? file)
        {
            var hasText = !string.IsNullOrWhiteSpace(message);
            var hasFile = file != null && file.Length > 0;

            if (!hasText && !hasFile)
                throw new ArgumentException("Message must contain text or a file.");

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            
            if (conversation == null)
            {
                _logger.LogWarning("Conversation with ID {ConversationId} not found.", conversationId);
                return null;
            }

            var msg = new Messages
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,    
                SenderId = senderId,
                Text = hasText ? message : null,
                TimeSent = DateTime.UtcNow
            };

            _context.Messages.Add(msg);

            if (hasFile)
            {
                var attachment = await _blobService.UploadMessageAttachmentAsync(file!, msg.Id, conversationId);

                _context.MessageAttachment.Add(attachment);
            }

            conversation.LastMessageTime = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            return msg;
        }
    }
}
