using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly HttpClient _httpClient;

        public WhatsAppService(
            MyDatabaseContext context,
            ILogger<WhatsAppService> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger  = logger;

            var apiKey = config["WASSENGER-API-KEY"]
                         ?? throw new InvalidOperationException(
                                "WASSENGER-API-KEY is missing in configuration");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "WASSENGER-API-KEY cannot be empty or whitespace.");

            // Use the named HttpClient configured in Program.cs
            _httpClient = httpClientFactory.CreateClient("Wassenger");
        }

        public async Task<IEnumerable<WhatsAppVoiceMessages>> GetAllMessagesAsync()
        {
            return await _context.WhatsAppVoiceMessages.ToListAsync();
        }

        public async Task<(bool Valid, bool Exists, string? Error)> ValidateContactAsync(string phone)
        {
            var payload = JsonSerializer.Serialize(new { phone });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            try
            {
                _logger.LogInformation("Calling /v1/numbers/exists with phone {Phone}", phone);
                var resp = await _httpClient.PostAsync("/v1/numbers/exists", content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return (false, false, body);

                using var doc = JsonDocument.Parse(body);
                var exists = doc.RootElement.GetProperty("exists").GetBoolean();
                return (true, exists, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating contact");
                return (false, false, ex.Message);
            }
        }

        public async Task SendAsync(Guid messageId, string phone)
        {
            var message = await _context.WhatsAppVoiceMessages.FindAsync(messageId);
            if (message == null)
                throw new InvalidOperationException($"Message {messageId} not found");

            var payload = JsonSerializer.Serialize(new
            {
                phone,
                media = new { url = message.FileUrl, format = "ptt" }
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending message {MessageId} to {Phone}", messageId, phone);
            var resp = await _httpClient.PostAsync("/v1/messages", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var apiId = doc.RootElement.GetProperty("id").GetString();
                if (!string.IsNullOrEmpty(apiId))
                {
                    message.WassengerMessageId = apiId;
                    message.DeliveryStatus     = "queued";
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning("SendAsync failed {Status} - {Body}", resp.StatusCode, body);
                throw new HttpRequestException(
                    $"Wassenger Send failed: {resp.StatusCode} - {body}");
            }
        }

        public async Task<string?> GetStatusAsync(Guid messageId)
        {
            var msg = await _context.WhatsAppVoiceMessages.FindAsync(messageId);
            return msg?.DeliveryStatus;
        }
    }
}
