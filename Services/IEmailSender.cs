namespace DotNetCoreSqlDb.Services;
using MimeKit;

public interface IEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default);

    Task ForwardAsync(
        string to,
        string subject,
        MimeMessage originalMessage,
        string? note = null,
        CancellationToken ct = default);

}
