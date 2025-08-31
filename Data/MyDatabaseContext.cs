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
        public MyDatabaseContext(DbContextOptions<MyDatabaseContext> options)
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

        public DbSet<DotNetCoreSqlDb.Models.WassengerApiLog> WassengerApiLog { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.ZoomMeetings> ZoomMeetings { get; set; } = default!;
        
        public DbSet<DotNetCoreSqlDb.Models.MailLog> MailLog { get; set; } = default!;
        
        public DbSet<DotNetCoreSqlDb.Models.SignInLog> SignInLog { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.StudentBalance> StudentBalance { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.StudentBalanceTransactionLog> StudentBalanceTransactionLog { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.Payer> Payer { get; set; } = default!;

        public DbSet<DotNetCoreSqlDb.Models.ZoomMeetingLog> ZoomMeetingLog { get; set; } = default!;


        // method defines model-level constraints like unique indexes
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enforce unique usernames in the database
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<StudentBalance>()
            .HasIndex(sb => new { sb.StudentId, sb.AssignmentId })
            .IsUnique();

        }
        

        /*[DbFunction("DIFFERENCE", IsBuiltIn = true)]
        public static int Difference(string s1, string s2) => throw new NotSupportedException();   // never executed*/
        

    }
}
