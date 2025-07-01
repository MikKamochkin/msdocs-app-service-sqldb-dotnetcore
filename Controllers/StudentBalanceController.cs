using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Services;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using DotNetCoreSqlDb.Helpers;
using System.Collections.Generic;   
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TimeZoneConverter;
using DotNetCoreSqlDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Internal;

namespace DotNetCoreSqlDb.Controllers
{
    public class StudentBalanceController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly LogHelper _logHelper;

        public StudentBalanceController(MyDatabaseContext context, LogHelper logHelper)
        {
            _context = context;
            _logHelper = logHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var payers = await _context.Payer
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToListAsync();

            ViewBag.Payers = payers;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentsByPayer(Guid payerId)
        {
            var payer = await _context.Payer
                .Where(p => p.Id == payerId)
                .FirstOrDefaultAsync();

            if (payer != null)
            {
                var students = await _context.Student
                    .Where(s => s.ID == payer.StudentId)
                    .ToListAsync();

                return Json(students);

            }
            return NotFound();

        }

        [HttpGet]
        public async Task<IActionResult> GoToStudent(Guid id)
        {
            // Redirect to your student-specific page
            return RedirectToAction("UpdateBalance", "StudentBalance", new { id = id });
        }

        [HttpGet]
        public async Task<IActionResult> UpdateBalance(Guid id)
        {
            var student = await _context.Student.FindAsync(id);

            if (student == null)
            {
                return NotFound();
            }

            var balances = await _context.StudentBalance
                .Where(b => b.StudentId == id)
                //.Include(a => a.Assignment)
                .ToListAsync();

            ViewBag.StudentName = student.Name;

            return View(balances);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToBalance(Guid balanceId, float unitsToAdd, float amountPaid,
        string adminNotes, string currency, string payerNotes, string paymentReference, string paymentType)
        {
            var balance = await _context.StudentBalance.FindAsync(balanceId);
              
            if (balance == null)
            {
                return NotFound();
            }

            balance.Balance += unitsToAdd;
            await _context.SaveChangesAsync();

            if (balance != null)
            {
                var student = await _context.Student.FindAsync(balance.StudentId);
                Guid assignmentId = balance.AssignmentId;
                await _logHelper.LogStudentBalanceAsync(student.ID, assignmentId, currency, paymentType, paymentReference, payerNotes, adminNotes, unitsToAdd, amountPaid);
            }
            

            return RedirectToAction(nameof(UpdateBalance), new { id = balance.StudentId });
        }

    }
}
