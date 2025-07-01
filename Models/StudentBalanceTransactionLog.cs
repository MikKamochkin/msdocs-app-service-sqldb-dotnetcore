using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotNetCoreSqlDb.Models
{
    public class StudentBalanceTransactionLog
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey("Student")]
        public Guid StudentId { get; set; }

        [ForeignKey("Assignment")]
        public Guid? AssignmentId { get; set; }

        [ForeignKey("Schedule")]
        public Guid? ScheduleId { get; set; }

        public DateTime? DateTime { get; set; }

        public float? CurrentBalance { get; set; }

        public float? TransactionAmount { get; set; }

        public float? AmountPaid { get; set; }

        public string? Currency { get; set; }

        public string? PaymentType { get; set; }

        public string? PaymentReference { get; set; }

        public string? PayerNotes { get; set; }

        public string? AdminNotes { get; set; }

        /* ─── Navigation references ─────────────────────── */
        public Student Student { get; set; } = null!;
        public Assignments Assignment { get; set; } = null!;
    }
}