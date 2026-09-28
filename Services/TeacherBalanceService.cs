// Services/DataConversionService.cs
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using DotNetCoreSqlDb.Data;                 
using DotNetCoreSqlDb.Services.Models;      
using DotNetCoreSqlDb.Models;


namespace DotNetCoreSqlDb.Services
{
    public class TeacherBalanceService : ITeacherBalanceService
    {
        private readonly MyDatabaseContext _context;
        private readonly ILogger<UpdateBalanceService> _logger;
        public TeacherBalanceService(MyDatabaseContext context, ILogger<UpdateBalanceService> logger)
        {
            _context = context;
            _logger = logger;
        }
        /*public async Task GetTeacherBalanceAsync(Guid teacherId)
        {
            return 0m;
        }*/

    }
}
