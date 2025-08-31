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
    [Authorize(Roles = "support, admin, assistant")]
    public class StudentBalanceController : Controller
    {
        private readonly MyDatabaseContext _context;
        private readonly LogHelper _logHelper;
        private readonly ILogger<StudentBalanceController> _logger;

        public StudentBalanceController(MyDatabaseContext context, LogHelper logHelper, ILogger<StudentBalanceController> logger)
        {
            _context = context;
            _logHelper = logHelper;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentsByPayer(Guid payerId)
        {
            var payer = await _context.Payer
                .Where(p => p.Id == payerId)
                .FirstOrDefaultAsync();
            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("support");

            if (isPrivileged)
            {
                if (payer != null)
                {
                    var students = await _context.Student
                        .Where(s => s.ID == payer.StudentId)
                        .ToListAsync();

                    return Json(students);

                }
                return NotFound();
            }
            else if (User.IsInRole("assistant"))
            {
                if (payer != null)
                {
                    var students = await _context.Student
                        .Where(s => s.ID == payer.StudentId && s.AccountingGroup == "S")
                        .ToListAsync();

                    return Json(students);

                }
                return NotFound();
            }
            else
            {
                return NotFound();
            }


        }

        [HttpGet]
        public async Task<IActionResult> SearchByPayer(string payerName)
        {
            if (string.IsNullOrWhiteSpace(payerName))
                return View("Index", payerName); // Return to view with error indication if needed

            var payers = await _context.Payer
                .Where(p => EF.Functions.Collate(p.Name, "Latin1_General_CS_AS") == payerName.Trim())
                .ToListAsync();

            var payerStudentIds = payers.Select(p => p.StudentId).Distinct();

            var isPrivileged = User.IsInRole("admin") || User.IsInRole("support");

            var studentsQuery = _context.Student.AsQueryable();

            if (User.IsInRole("assistant") && !isPrivileged)
                studentsQuery = studentsQuery.Where(s => s.AccountingGroup == "S");

            var students = await studentsQuery
                .Where(s => payerStudentIds.Contains(s.ID))
                .Select(s => new SelectListItem
                {
                    Value = s.ID.ToString(),
                    Text = s.Name
                }).ToListAsync();

            ViewBag.Students = students;
            ViewBag.PayerNameSearched = payerName;
            return View("Index", payerName);
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
                .Include(a => a.Assignment)
                    .ThenInclude(t => t.Teacher)
                .ToListAsync();

            ViewBag.StudentName = student.Name;

            var transactions = await _context.StudentBalanceTransactionLog
                .Where(s => s.ScheduleId == null && s.StudentId == id)
                .Include(s => s.Assignment)
                    .ThenInclude(s => s.Teacher)
                .OrderByDescending(s => s.DateTime)
                .Take(10)
                .ToListAsync();

            ViewBag.Transactions = transactions;

            var items = DropdownOptions.PayUnitTypes?.Select(x => new SelectListItem
            {
                Value = x.Value,
                Text = x.Text,
                Selected = (x.Value == "CAD")
            }).ToList();

            ViewBag.PayUnitTypes = items;
            ViewBag.DefaultCurrency = "CAD";
            ViewBag.DefaultPaymentType = "INTERAC";

            return View(balances);
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionDetails(Guid transactionLogId)
        {
            //_logger.LogInformation("get transactiondetails for id: " + transactionLogId);
            var tx = await _context.StudentBalanceTransactionLog
                .Include(t => t.Assignment).ThenInclude(a => a.Teacher)
                .FirstOrDefaultAsync(t => t.Id == transactionLogId);

            if (tx == null) return NotFound();


            return Json(new
            {
                DateTime = tx.DateTime?.ToString("g"),
                Teacher = tx.Assignment?.Teacher?.Name,
                Units = tx.TransactionAmount,
                AmountPaid = tx.AmountPaid,
                Currency = tx.Currency,
                PayerNotes = tx.PayerNotes,
                AdminNotes = tx.AdminNotes,
                Reference = tx.PaymentReference,
                Type = tx.PaymentType
            });
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

        [HttpGet]
        public async Task<IActionResult> BalancesList(Guid id)
        {

            var assignments = await _context.Assignments
                .Where(a => a.GroupId == id && a.IsActive == true)
                .Include(a => a.Teacher)
                .Include(a => a.Group)
                .ToListAsync();

            ViewBag.Assignments = assignments;

            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> BalancesForAssignment(Guid assignmentId)
        {
            // INNER JOIN to get Student.Name without a viewmodel
            var rows = await _context.StudentBalance
                .Where(b => b.AssignmentId == assignmentId)
                .Join(
                    _context.Student,
                    b => b.StudentId,
                    s => s.ID,
                    (b, s) => new
                    {
                        StudentId = b.StudentId,
                        StudentName = s.Name,
                        AssignmentId = b.AssignmentId,
                        Balance = b.Balance
                    }
                )
                .OrderByDescending(r => r.Balance)
                .ToListAsync();

            return Json(rows);
        }


    }
}
