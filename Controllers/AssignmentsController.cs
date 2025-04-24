using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.InteropServices;
using System.Data;

namespace DotNetCoreSqlDb.Controllers
{
    [Authorize(Roles = "support")]
    public class AssignmentsController : Controller
    {
        private readonly MyDatabaseContext _context;

        public AssignmentsController(MyDatabaseContext context)
        {
            _context = context;
        }

        // GET: Assignments
        public async Task<IActionResult> Index()
        {
            var assignments = await _context.Assignments
                .Where(a => a.IsActive == true)
                .Include(a => a.Group)
                .Include(a => a.Teacher)
                .ToListAsync();
            return View(assignments);
        }
    }
}
