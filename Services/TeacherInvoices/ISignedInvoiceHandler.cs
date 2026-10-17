using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Services;
using System.Net;

namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public interface ISignedInvoiceHandler
{
    Task HandleAsync(
        SignedTeacherInvoice invoice,
        CancellationToken cancellationToken);
}

// It may be called more than once.
// Use SubmissionId as your idempotency key.
public sealed class SignedInvoiceHandler : ISignedInvoiceHandler
{
    private readonly IEmailSender _mailer;
    private readonly ILogger<SignedInvoiceHandler> _logger;

    public SignedInvoiceHandler(IEmailSender mailer, ILogger<SignedInvoiceHandler> logger)
    {
        _mailer = mailer;
        _logger = logger;
    }

    public async Task HandleAsync(
        SignedTeacherInvoice invoice,
        CancellationToken cancellationToken)
    {
        // The signed PDF is available as invoice.PdfBytes.

        string invoiceNumber = invoice.Invoice.Input.InvoiceNumber;
        string teacherName = invoice.Invoice.Input.Name;

        string subject = $"Signed teacher invoice - {invoiceNumber}";

        string htmlBody = $"""
            <p>A teacher invoice has been signed.</p>
            <p><strong>Teacher:</strong> {WebUtility.HtmlEncode(teacherName)}</p>
            <p><strong>Invoice number:</strong> {WebUtility.HtmlEncode(invoiceNumber)}</p>
            <p>The signed invoice PDF is attached.</p>
            """;

        //_logger.LogInformation("Sending email with pdf");
        
        await _mailer.SendWithAttachmentAsync(
            to: "kamochkin@gmail.com",
            subject: subject,
            htmlBody: htmlBody,
            attachmentBytes: invoice.PdfBytes,
            attachmentFileName: invoice.FileName,
            ct: cancellationToken
        );
    }
}