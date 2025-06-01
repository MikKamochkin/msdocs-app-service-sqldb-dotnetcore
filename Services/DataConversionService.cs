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
using DotNetCoreSqlDb.Data;                 // your EF DbContext namespace
using DotNetCoreSqlDb.Services.Models;      // the two CSV‐row classes from above
using DotNetCoreSqlDb.Models;


namespace DotNetCoreSqlDb.Services
{
    public class DataConversionService : IDataConversionService
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _trackingFilePath;
        private readonly DateTime _defaultDate = DateTime.Parse("2025-04-09T00:00:28.5983573", null, DateTimeStyles.RoundtripKind);

        public DataConversionService(IWebHostEnvironment env)
        {
            _env = env;

            // Put the tracking JSON under wwwroot/App_Data so it's not publicly served.
            // You can adjust this to any folder you like.
            var contentRoot = _env.ContentRootPath;
            var appDataFolder = Path.Combine(contentRoot, "wwwroot", "App_Data");
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            _trackingFilePath = Path.Combine(appDataFolder, "last_processed_date.json");
        }

        public async Task<(byte[] accountCsv, byte[] studentCsv)> GenerateCsvFilesAsync(
            IEnumerable<Student> students,
            IEnumerable<Contact> contacts)
        {
            // 1) Load or initialize tracking date
            DateTime filterAfter;
            if (File.Exists(_trackingFilePath))
            {
                try
                {
                    using var jsonStream = File.OpenRead(_trackingFilePath);
                    var doc = await JsonDocument.ParseAsync(jsonStream);
                    if (doc.RootElement.TryGetProperty("last_processed_date", out var dateEl) &&
                        DateTime.TryParse(dateEl.GetString(), null, DateTimeStyles.RoundtripKind, out var parsed))
                    {
                        filterAfter = parsed;
                    }
                    else
                    {
                        filterAfter = _defaultDate;
                    }
                }
                catch
                {
                    filterAfter = _defaultDate;
                }
            }
            else
            {
                filterAfter = _defaultDate;
            }

            // 2) Filter students by CreatedDate > filterAfter
            var filteredStudents = students
                .Where(s => s.CreatedDate.ToUniversalTime() > filterAfter.ToUniversalTime())
                .OrderBy(s => s.CreatedDate)
                .ToList();

            // 3) Build a lookup of email contacts for those filtered students
            var emailMapping = contacts
                .Where(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
                .Where(c => filteredStudents.Any(s => s.ID == c.StudentID))
                .GroupBy(c => c.StudentID)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value   // if multiple emails exist, just take the first
                );

            // 4) Build lists of AccountCsvRow and StudentCsvRow
            var accountRows = new List<AccountCsvRow>();
            var studentRows = new List<StudentCsvRow>();

            foreach (var stu in filteredStudents)
            {
                var accountId = Guid.NewGuid().ToString();
                var emailValue = emailMapping.ContainsKey(stu.ID) ? emailMapping[stu.ID] : string.Empty;

                // AccountCsvRow
                accountRows.Add(new AccountCsvRow
                {
                    Id           = accountId,
                    Name         = string.IsNullOrWhiteSpace(stu.ParentOrEmployer)
                                     ? stu.Name
                                     : stu.ParentOrEmployer,
                    Balance      = 0m,
                    Status       = "",
                    Email        = emailValue,
                    TotalCharge  = 0m,
                    TotalPay     = 0m
                });

                // StudentCsvRow
                studentRows.Add(new StudentCsvRow
                {
                    Id           = stu.ID,
                    Name         = stu.Name,
                    AccountId    = accountId,
                    Status       = "Active",
                    Email        = "",   // always blank
                    TotalCharge  = 0m
                });
            }

            // 5) Serialize each list to CSV (in memory)
            byte[] accountCsvBytes, studentCsvBytes;
            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(accountRows);
                await writer.FlushAsync();
                accountCsvBytes = ms.ToArray();
            }

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(studentRows);
                await writer.FlushAsync();
                studentCsvBytes = ms.ToArray();
            }

            // 6) Determine next “last_processed_date”
            DateTime nextStart;
            string latestStudentName;
            string latestStudentDateStr;

            if (filteredStudents.Count > 0)
            {
                var latest = filteredStudents.Max(s => s.CreatedDate.ToUniversalTime());
                nextStart = latest.ToUniversalTime().AddSeconds(1);
                var latestStu = filteredStudents
                    .First(s => s.CreatedDate.ToUniversalTime() == latest);
                latestStudentName = latestStu.Name;
                latestStudentDateStr = latest.ToString("o");
            }
            else
            {
                // no new students: keep filterAfter as-is
                nextStart = filterAfter.ToUniversalTime();
                latestStudentName = "None";
                latestStudentDateStr = filterAfter.ToString("o");
            }

            // 7) Overwrite tracking JSON on disk
            var trackingObj = new
            {
                last_processed_date = nextStart.ToString("o"),
                last_run_date       = DateTime.UtcNow.ToString("o"),
                processed_count     = filteredStudents.Count,
                latest_student      = new {
                    name         = latestStudentName,
                    created_date = latestStudentDateStr
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            using (var fs = File.Create(_trackingFilePath))
            {
                await JsonSerializer.SerializeAsync(fs, trackingObj, options);
            }

            return (accountCsvBytes, studentCsvBytes);
        }
    }
}
