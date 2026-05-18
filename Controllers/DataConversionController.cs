using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;      
using DotNetCoreSqlDb.Services;  
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class DataConversionController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly IDataConversionService _conversionSvc;
        private readonly IMemoryCache _cache;

        public DataConversionController(
            MyDatabaseContext context,
            IDataConversionService conversionSvc,
            IMemoryCache cache)
        {
            _context = context;
            _conversionSvc = conversionSvc;
            _cache = cache;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunConversion()
        {
            var students = await _context.Student.ToListAsync();
            var contacts = await _context.Contact.ToListAsync();

            var (accountCsvBytes, studentCsvBytes) =
                await _conversionSvc.GenerateCsvFilesAsync(students, contacts);

            var token = Guid.NewGuid().ToString("N");

            _cache.Set($"acct:{token}", accountCsvBytes, TimeSpan.FromMinutes(10));
            _cache.Set($"stud:{token}", studentCsvBytes, TimeSpan.FromMinutes(10));

            return RedirectToAction(nameof(DownloadLinks), new { token });
        }

        [HttpGet]
        public IActionResult DownloadLinks(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(Index));

            // If either is missing, token expired/invalid
            if (!_cache.TryGetValue($"acct:{token}", out _)) return RedirectToAction(nameof(Index));
            if (!_cache.TryGetValue($"stud:{token}", out _)) return RedirectToAction(nameof(Index));

            ViewBag.Token = token;
            return View();
        }

        [HttpGet]
        public IActionResult DownloadAccountCsv(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return NotFound();

            if (!_cache.TryGetValue($"acct:{token}", out byte[] bytes)) return NotFound();
            return File(bytes, "text/csv", "target_account.csv");
        }

        [HttpGet]
        public IActionResult DownloadStudentCsv(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return NotFound();

            if (!_cache.TryGetValue($"stud:{token}", out byte[] bytes)) return NotFound();
            return File(bytes, "text/csv", "target_student.csv");
        }

    }
}
