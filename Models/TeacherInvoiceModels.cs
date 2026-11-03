using System.ComponentModel.DataAnnotations;

namespace DotNetCoreSqlDb.Models;

// DTOs only: do NOT add these to MyDatabaseContext.
public sealed class TeacherInvoiceInput
{
    [Required, DataType(DataType.Date)] public DateTime StartDate { get; set; }
    [Required, DataType(DataType.Date)] public DateTime EndDate { get; set; }
    [Required, StringLength(100)] public string InvoiceNumber { get; set; } = "";
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [StringLength(200)] public string? StreetAddress { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? ProvinceOrState { get; set; }
    [StringLength(30)] public string? PostalCode { get; set; }
    [StringLength(100)] public string? Country { get; set; }
    [StringLength(50)] public string? PhoneNumber { get; set; }
    [EmailAddress, StringLength(254)] public string? ETransferEmail { get; set; }
    [StringLength(1000)]
    public string? CorrectionDescription { get; set; }

    [DataType(DataType.Date)]
    public DateTime? CorrectionDate { get; set; }
    public decimal? CorrectionAmount { get; set; }

    [Required(ErrorMessage = "Select the statement that applies to this invoice.")]
    [RegularExpression("^(Canadian|NonResident)$", ErrorMessage = "Select the statement that applies to this invoice.")]
    public string? ContractorType { get; set; }

    [StringLength(5000)] public string? Note { get; set; }

    public bool HasCorrection =>
        !string.IsNullOrWhiteSpace(CorrectionDescription) ||
        CorrectionDate.HasValue ||
        CorrectionAmount.HasValue;
}
public static class ContractorDeclaration
{
    public const string Canadian = "Canadian";
    public const string NonResident = "NonResident";
    public const string CanadianTitle = "Canadian Contractor";
    public const string NonResidentTitle = "Non-Resident Contractor";
    public const string CanadianStatement = "I confirm that I am a resident of Canada for Canadian income-tax purposes and that all services covered by this invoice were performed while I was physically located in Canada.";
    public const string NonResidentStatement = "I confirm that I am not a resident of Canada for Canadian income-tax purposes and that all services covered by this invoice were performed while I was physically located outside Canada.";

    public static string Title(string? type) => type == Canadian ? CanadianTitle : NonResidentTitle;
    public static string Statement(string? type) => type == Canadian ? CanadianStatement : NonResidentStatement;
}
public sealed record InvoiceLesson(Guid ScheduleId, DateTime TorontoDateTime,
    string GroupName, int DurationMinutes, decimal Amount);
public sealed class TeacherInvoiceSnapshot
{
    public Guid TeacherId { get; set; }
    public TeacherInvoiceInput Input { get; set; } = new();
    public DateTime InvoiceDate { get; set; }
    public DateTime PaymentDue { get; set; }
    public string Currency { get; set; } = "";
    public List<InvoiceLesson> Lessons { get; set; } = new();
    public decimal Total =>
    Lessons.Sum(x => x.Amount) + (Input.CorrectionAmount ?? 0m);
}
public sealed class TeacherInvoicePage
{
    public TeacherInvoiceInput Input { get; set; } = new();
    public TeacherInvoiceSnapshot? Preview { get; set; }
    public string? SigningUrl { get; set; }
    public string? Token { get; set; }
}
public sealed record InvoiceSigningSession(long SubmissionId, string SigningUrl, string Token);
public sealed record SignedTeacherInvoice(long SubmissionId, TeacherInvoiceSnapshot Invoice,
    byte[] PdfBytes, string FileName, DateTimeOffset CompletedAt, string? AuditLogUrl);
public sealed class InvoiceTicket
{
    public long SubmissionId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public TeacherInvoiceSnapshot Invoice { get; set; } = new();
}
public sealed class InvoiceValidationException(string message) : Exception(message) { }
public sealed class InvoiceNotCompletedException() : Exception("The signed PDF is not ready yet.") { }
