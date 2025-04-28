// Controllers/WhatsAppController.cs (updated)
using System;
using System.Threading.Tasks;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class WhatsAppController : Controller
    {
        private readonly IWhatsAppService _svc;
        public WhatsAppController(IWhatsAppService svc) => _svc = svc;

        public async Task<IActionResult> Index() => View(await _svc.GetAllMessagesAsync());

        [HttpGet]
        public async Task<IActionResult> ValidateContact(string phone)
        {
            var (valid, exists, err) = await _svc.ValidateContactAsync(phone);
            return valid ? Json(new { valid = true, exists })
                         : StatusCode(500, new { valid = false, error = err });
        }

        [HttpPost]
        public async Task<IActionResult> Send(Guid id, string phone)
        {
            await _svc.SendAsync(id, phone);
            return Ok();
        }
    }
}
