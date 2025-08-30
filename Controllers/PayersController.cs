// Controllers/PayersController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Authorization;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class PayersController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<PayersController> _logger;

        public PayersController(MyDatabaseContext context, ILogger<PayersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Payers/Create
        public async Task<IActionResult> Create()
        {
            /*
            var payer = new Payer
            {
                Id = Guid.NewGuid()  // Initialize the ID here if needed
            };
            await PopulateDropdowns(payer);
            return View(payer);*/
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payer payer)
        {

            payer.Id = Guid.NewGuid();
            _context.Payer.Add(payer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Payers
        public async Task<IActionResult> Index()
        {
            /*
            var payers = await _context.Payer
                .Include(p => p.Student)
                .AsSplitQuery()
                .ToListAsync();
            return View(payers);*/
            return NotFound();
        }

        // Shared helper to populate dropdown lists
        private async Task PopulateDropdowns(Payer model)
        {
            ViewBag.Students = new SelectList(
                await _context.Student.OrderBy(g => g.Name).ToListAsync(),
                "ID", "Name",  // must match the exact casing in the Student model
                model.StudentId
            );
        }

    }
}
