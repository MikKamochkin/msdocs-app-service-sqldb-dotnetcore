using System.Threading.Tasks;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Controllers
{
    [ApiController]
    [Route("webhooks/wassenger")]
    public class WebhookController : ControllerBase
    {
        private readonly MyDatabaseContext _ctx;
        private readonly ILogger<WebhookController> _logger;
        private readonly IHubContext<WhatsAppHub> _hub;
        private readonly string _expectedSecret;

        public WebhookController(
            MyDatabaseContext ctx,
            ILogger<WebhookController> logger,
            IHubContext<WhatsAppHub> hub,
            IConfiguration config)
        {
            _ctx             = ctx;
            _logger          = logger;
            _hub             = hub;
            _expectedSecret  = config["WASSENGER-WEBHOOK-EXPECTED-SECRET"]
                               ?? throw new ArgumentNullException(
                                      "WASSENGER-WEBHOOK-EXPECTED-SECRET");
        }

        public class WebhookEnvelope
        {
            public string? Id   { get; set; }
            public string? Object { get; set; }
            public string? Event { get; set; }
            public long? Created { get; set; }
            public WebhookData? Data { get; set; }
        }
        public class WebhookData
        {
            public string? Id             { get; set; }
            public string? DeliveryStatus { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Receive(
            [FromHeader(Name = "X-Wassenger-Secret")] string? headerSecret,
            [FromQuery(Name = "secret")]    string? querySecret,
            [FromBody]                      WebhookEnvelope payload)
        {
            // Support secret via header or ?secret= query parameter
            var incomingSecret = headerSecret ?? querySecret;
            if (incomingSecret != _expectedSecret)
                return Unauthorized();

            if (payload?.Data?.Id == null ||
                string.IsNullOrEmpty(payload.Data.DeliveryStatus))
                return Ok();

            var msg = await _ctx.WhatsAppVoiceMessages
                                .FirstOrDefaultAsync(m =>
                                     m.WassengerMessageId == payload.Data.Id);
            if (msg == null) return Ok();

            msg.DeliveryStatus = payload.Data.DeliveryStatus;
            await _ctx.SaveChangesAsync();

            await _hub.Clients.All
                  .SendAsync("statusUpdated", msg.Id, msg.DeliveryStatus);
            return Ok();
        }
    }
}
