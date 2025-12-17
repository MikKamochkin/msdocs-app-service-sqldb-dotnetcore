using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using DotNetCoreSqlDb.Settings;

namespace DotNetCoreSqlDb.Services;


public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailSettings _cfg;

    public MailKitEmailSender(IOptions<EmailSettings> options) =>
        _cfg = options.Value;

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        // 1. Build MIME message
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_cfg.DisplayName, _cfg.Username));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        email.Body = builder.ToMessageBody();

        // 2. Send
        using var smtp = new SmtpClient();

        var socketOpt = _cfg.UseSsl
            ? SecureSocketOptions.SslOnConnect          // port 465
            : SecureSocketOptions.StartTlsWhenAvailable; // port 587

        await smtp.ConnectAsync(_cfg.Host, _cfg.Port, socketOpt, ct);
        await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password, ct);
        await smtp.SendAsync(email, ct);
        await smtp.DisconnectAsync(true, ct);
    }

    public async Task ForwardAsync(
        string to,
        string subject,
        MimeMessage originalMessage,
        string? note = null,
        CancellationToken ct = default)
    {
        var forward = new MimeMessage();
        forward.From.Add(new MailboxAddress(_cfg.DisplayName, _cfg.Username));
        forward.To.Add(MailboxAddress.Parse(to));
        forward.Subject = subject;

        var builder = new BodyBuilder();

        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.TextBody = note;
        }

        // ✅ Proper RFC-822 attachment
        var rfc822 = new MessagePart
        {
            Message = originalMessage
        };

        builder.Attachments.Add(rfc822);

        forward.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        var socketOpt = _cfg.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await smtp.ConnectAsync(_cfg.Host, _cfg.Port, socketOpt, ct);
        await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password, ct);
        await smtp.SendAsync(forward, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
