using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;


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

            const int maxTries = 10;
            for (int attempt = 1; attempt <= maxTries; attempt++)
            {
                var resp = await _httpClient.PostAsync("/v1/numbers/exists", content);
                var body = await resp.Content.ReadAsStringAsync();
                await LogApiAsync($"POST /v1/numbers/exists → {payload}", body);

                if (resp.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Wait a bit longer each time
                    await Task.Delay(200 * attempt);
                    continue;
                }

                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    return (true,
                            doc.RootElement.GetProperty("exists").GetBoolean(),
                            null);
                }

                // 400 or other errors → treat as “invalid number”
                return (true, false, null);
            }

            // still offline after retries
            return (false, false, "WhatsApp session offline; please try again soon");
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
            await LogApiAsync($"POST /v1/messages → {payload}", body);

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

        private async Task LogApiAsync(string request, string response)
        {
            var log = new WassengerApiLog {
                Request = request,
                Time = DateTime.UtcNow,
                Response = response
            };
            _context.WassengerApiLog.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
