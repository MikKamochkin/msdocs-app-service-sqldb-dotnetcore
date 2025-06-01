// /Controllers/DataConversionController.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;      // your DbContext namespace
using DotNetCoreSqlDb.Services;  // <-- so IDataConversionService resolves
using DotNetCoreSqlDb.Models;    // <-- so Student and Contact resolve (if needed)

namespace DotNetCoreSqlDb.Controllers
{
    public class DataConversionController : Controller
    {
        private readonly MyDatabaseContext      _context;
        private readonly IDataConversionService _conversionSvc;

        public DataConversionController(
            MyDatabaseContext context,
            IDataConversionService conversionSvc)
        {
            _context       = context;
            _conversionSvc = conversionSvc;
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

            TempData["AccountCsv"] = System.Convert.ToBase64String(accountCsvBytes);
            TempData["StudentCsv"] = System.Convert.ToBase64String(studentCsvBytes);

            return RedirectToAction(nameof(DownloadLinks));
        }

        [HttpGet]
        public IActionResult DownloadLinks()
        {
            if (!TempData.ContainsKey("AccountCsv") ||
                !TempData.ContainsKey("StudentCsv"))
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [HttpGet]
        public IActionResult DownloadAccountCsv()
        {
            if (!TempData.ContainsKey("AccountCsv"))
                return NotFound();

            var base64 = TempData["AccountCsv"] as string;
            var bytes = System.Convert.FromBase64String(base64);
            TempData.Keep("AccountCsv"); 
            return File(bytes, "text/csv", "target_account.csv");
        }

        [HttpGet]
        public IActionResult DownloadStudentCsv()
        {
            if (!TempData.ContainsKey("StudentCsv"))
                return NotFound();

            var base64 = TempData["StudentCsv"] as string;
            var bytes = System.Convert.FromBase64String(base64);
            TempData.Keep("StudentCsv");
            return File(bytes, "text/csv", "target_student.csv");
        }
    }
}
