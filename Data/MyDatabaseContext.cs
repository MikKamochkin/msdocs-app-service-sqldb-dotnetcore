using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Data
{
    public class MyDatabaseContext : DbContext
    {
        public MyDatabaseContext (DbContextOptions<MyDatabaseContext> options)
            : base(options)
        {
        }

        public DbSet<DotNetCoreSqlDb.Models.Todo> Todo { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Student> Student { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Contact> Contact { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Notes> Note { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.User> User { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Teacher> Teacher { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Group> Group { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Assignments> Assignments { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Schedule> Schedule { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.StudentGroupComposition> StudentGroupComposition { get; set; } = default!;    

        public DbSet<DotNetCoreSqlDb.Models.WhatsAppVoiceMessages> WhatsAppVoiceMessages { get; set; } = default!;
        
    }
}
