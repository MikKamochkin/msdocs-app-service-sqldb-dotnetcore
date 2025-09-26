using System;

namespace DotNetCoreSqlDb.ViewModels
{
    public record TimelineItem
    {
        public DateTime? DateTime { get; init; }
        public string? Kind { get; init; } = ""; // "Lesson" | "Transaction"

        // Lesson fields
        public Guid? ScheduleId { get; init; }
        public Guid? AssignmentId { get; init; }
        public string? Status { get; init; }
        public int? DurationMinutes { get; init; }

        // Transaction fields
        public Guid? TransactionId { get; init; }
        public float? Amount { get; init; }
        public string? Currency { get; init; }
        public string? PaymentType { get; init; }
        public string? TeacherName { get; init; }
    }
}
