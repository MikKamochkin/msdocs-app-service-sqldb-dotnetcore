using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class WhatsAppController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<WhatsAppController> _logger;
        private const string _bearer = "4440b2fb9d48747b656dc2da886fcc3d68331d1a0f41b703e67fc490493dc5b9c62ad15e1be09e8d";

        public WhatsAppController(MyDatabaseContext context, ILogger<WhatsAppController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(Guid? sentId)
        {
            var voiceMessages = await _context.WhatsAppVoiceMessages.ToListAsync();
            ViewBag.SentId = sentId?.ToString();
            return View(voiceMessages);
        }

        /// <summary>
        /// Validate a phone number against WhatsApp registry before sending.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ValidateContact(string phone)
        {
            _logger.LogInformation("➡️ [Server] ValidateContact called for phone: {Phone}", phone);

            using var http = new HttpClient { BaseAddress = new Uri("https://api.wassenger.com") };

            // the docs use a custom “Token” header rather than Bearer
            http.DefaultRequestHeaders.Add("Token", _bearer);

            var payloadObj = new { phone = phone };
            var json        = JsonSerializer.Serialize(payloadObj);
            _logger.LogInformation("📤 [Server] POST /v1/numbers/exists payload: {Payload}", json);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage resp;
            string respBody;

            try
            {
                resp     = await http.PostAsync("/v1/numbers/exists", content);
                respBody = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Server] Exception calling /v1/numbers/exists");
                return StatusCode(500, new { valid = false, error = "Server exception" });
            }

            _logger.LogInformation(
                "🔄 [Server] /v1/numbers/exists returned HTTP {Code}\n📥 Body: {Body}",
                (int)resp.StatusCode,
                respBody
            );

            if (!resp.IsSuccessStatusCode)
            {
                // pass through the upstream error so you can debug in the client
                return StatusCode((int)resp.StatusCode, new { valid = false, error = respBody });
            }

            try
            {
                using var doc = JsonDocument.Parse(respBody);
                var exists = doc.RootElement.GetProperty("exists").GetBoolean();
                _logger.LogInformation("✅ [Server] Number exists? {Exists}", exists);

                // return a simple “valid” flag to the client
                return Json(new { valid = exists, exists });
            }
            catch (JsonException je)
            {
                _logger.LogError(je, "❌ [Server] JSON parse error on /v1/numbers/exists response");
                return StatusCode(500, new { valid = false, error = "Parse error" });
            }
        }



        [HttpPost]
        public async Task<IActionResult> Send(Guid id, string phone)
        {
            // by the time we get here, JS has already validated phone
            var message = await _context.WhatsAppVoiceMessages.FindAsync(id);
            if (message == null) return NotFound();

            var url = message.FileUrl;
            var payload = new
            {
                phone,
                media = new { url, format = "ptt" }
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.wassenger.com") };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _bearer);

            try
            {
                var response     = await httpClient.PostAsync("/v1/messages", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var messageId = doc.RootElement.GetProperty("id").GetString();
                    message.WassengerMessageId = messageId;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to send: {Code} - {Body}",
                                       response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during Send");
            }

            return Ok(); // JS will handle continuation
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus(Guid id)
        {
            var message = await _context.WhatsAppVoiceMessages.FindAsync(id);
            if (message == null || string.IsNullOrEmpty(message.WassengerMessageId))
                return NotFound("No message ID available.");

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.wassenger.com") };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _bearer);

            var resp = await httpClient.GetAsync($"/v1/messages/{message.WassengerMessageId}");
            var body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
    }
}
