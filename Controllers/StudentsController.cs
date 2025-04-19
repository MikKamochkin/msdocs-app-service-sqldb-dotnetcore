using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize]
    public class StudentsController : Controller
    {
        private readonly MyDatabaseContext _context;

        public StudentsController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Students/Index
        public async Task<IActionResult> Index()
        {
            // Get the current user's ID from their claims
            var userId = User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            // Find the student record associated with this user
            var student = await _context.Student
                .Include(s => s.Contacts)
                .FirstOrDefaultAsync(s => s.ID.ToString() == userId);

            if (student == null)
                return NotFound();

            return View(student);
        }
    }
}
