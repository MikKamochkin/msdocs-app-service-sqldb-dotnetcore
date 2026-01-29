using System;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using DotNetCoreSqlDb.Settings;
using DotNetCoreSqlDb.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Helpers;

namespace DotNetCoreSqlDb.Services
{
    public class ImapEmailReader : IEmailInboxReader
    {
        private readonly EmailInboxSettings _settings;
        private readonly ILogger<ImapEmailReader> _logger;
        private readonly MyDatabaseContext _context;
        private readonly IUpdateBalanceService _updateBalanceSvc;
        private readonly IEmailSender _mailer;
        private readonly LogHelper _logHelper;

        public ImapEmailReader(
            IOptions<EmailInboxSettings> options,
            ILogger<ImapEmailReader> logger,
            MyDatabaseContext context,
            IUpdateBalanceService updateBalanceSvc,
            IEmailSender mailer,
            LogHelper logHelper)
        {
            _settings = options.Value;
            _logger   = logger;
            _context = context;
            _updateBalanceSvc = updateBalanceSvc;
            _mailer = mailer;
            _logHelper = logHelper;
        }

        public async Task CheckInboxAsync()
        {
            using var client = new ImapClient();

            try
            {
                /*_logger.LogInformation(
                    "Connecting to IMAP {Host}:{Port} as {User}",
                    _settings.Host, _settings.Port, _settings.Username);*/

                var secureOption = _settings.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(_settings.Host, _settings.Port, secureOption);
                await client.AuthenticateAsync(_settings.Username, _settings.Password);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);

                var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

                if (uids.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} unread emails in inbox for {User}.",
                        uids.Count, _settings.Username);
                }

                foreach (var uid in uids)
                {
                    var message = await inbox.GetMessageAsync(uid);

                    try
                    {
                        await ProcessMessageAsync(message);
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing email with subject {Subject} for {User}",
                            message.Subject, _settings.Username);
                    }
                }

                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error while checking IMAP inbox for {User}", _settings.Username);
                try { await client.DisconnectAsync(true); } catch { }
            }
        }

        private async Task ProcessMessageAsync(MimeMessage message)
        {
            var from     = message.From.ToString();
            var subject  = message.Subject ?? "";
            var textBody = message.TextBody ?? "";
            var htmlBody = message.HtmlBody ?? "";
            var date = message.Date;

            bool isEtransfer = IsEtransfer(from);
            if (isEtransfer)
            {
                await HandleEtransferEmail(textBody, subject, message);
            }
            else
            {
                await _mailer.SendAsync(
                        to: "torontofrench02@gmail.com",
                        subject: "Non Etransfer email",
                        htmlBody: "Subject: " + subject + "\nBody:\n" + textBody);
                
                await _logHelper.LogEmailPaymentsAsync(false, "Not an Etransfer", subject);
                return;
            }
            return;
        }

        private bool IsEtransfer(string from)
        {
            if (string.IsNullOrWhiteSpace(from))
                return false;

            from = from.ToLowerInvariant();

            return from.Contains("notify@payments.interac.ca", StringComparison.OrdinalIgnoreCase);
        }

        private async Task SendForManual(MimeMessage message, string error)
        {
            await _mailer.ForwardAsync(
                to: "torontofrench02@gmail.com",
                subject: "Automatic E-Transfer Handling Failed",
                originalMessage: message,
                note: $"Reason: {error}"
            );

            var subject = message.Subject ?? "";
            await _logHelper.LogEmailPaymentsAsync(false, error, subject);
            return;
        }

        private async Task HandleEtransferEmail(string body, string subject, MimeMessage message)
        {
            if (body == null)
            {
                string error = "Failed because body of email is null";
                await SendForManual(message, error);
                return;
            } //throw new ArgumentNullException(nameof(body));

            var text = body.Replace("\r\n", "\n");

            var sentFromMatch = Regex.Match(
                subject,
                @"from\s+(.+?)\s+and it has been",
                RegexOptions.Multiline);

            string sentFrom = sentFromMatch.Success
                ? sentFromMatch.Groups[1].Value.Trim()
                : "";

            var payers = await _context.Payer
                .Include(p => p.Student)
                .Where(p => EF.Functions.Collate(p.Name, "Latin1_General_CS_AS") == sentFrom)
                .ToListAsync();

            if (payers.Count() > 1)
            {
                string error = "Failed because there are more than 1 payers";
                await SendForManual(message, error);
                return;
            }

            if (payers == null || payers.Count() == 0)
            {
                string error = "Failed because payer not found";
                await SendForManual(message, error);
                return;
            } 
            
            var payer = payers.FirstOrDefault();
            
            var student = payer!.Student;

            var balances = await _context.StudentBalance
                    .Include(sb => sb.Assignment)
                        .Where(a => a.Assignment.IsActive)
                    .Where(sb => sb.StudentId == student!.ID)
                    .ToListAsync();

            if (balances == null || balances.Count() == 0)
            {
                string error = "Failed because balance doesn't exist";
                await SendForManual(message, error);
                return;
            } 

            if (balances.Count() > 1)
            {
                string error = "Failed because there are more than 1 balances";
                await SendForManual(message, error);
                return;
            } 
               
            var balance = balances.FirstOrDefault();

            var assignment = balance?.Assignment;

            if (assignment == null)
            {
                string error = "Failed because assignment doesn't exist";
                await SendForManual(message, error);
                return;
            } 

            var lessonCost = assignment.StudentUnitCost;

            var amountMatch = Regex.Match(
                text,
                @"Amount:\s*\$?([\d,]+\.\d{2})\s*\(([A-Z]{3})\)",
                RegexOptions.Multiline);

            float amount = 0f;
            string currency = "";

            if (amountMatch.Success)
            {
                var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
                float.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
                currency = amountMatch.Groups[2].Value;
            }
            else
            {
                // fallback: first $xx.xx
                var fallback = Regex.Match(text, @"\$([\d,]+\.\d{2})");
                if (fallback.Success)
                {
                    var amountStr = fallback.Groups[1].Value.Replace(",", "");
                    float.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
                }
            }

            int unitsToAdd;

            if (lessonCost == 55 && amount == 250)
            {
                unitsToAdd = 5;
            }
            else if (lessonCost == 55 && amount == 500)
            {
                unitsToAdd = 10;
            }
            else
            {   
                if (amount % lessonCost != 0)
                {
                    string error = "Failed because amount paid isnt divisble by lesson cost";
                    await SendForManual(message, error);
                    return;
                } 
                unitsToAdd = (int)(amount / lessonCost);
            }

            var messageMatch = Regex.Match(
                text,
                @"Message:\s*(.+)",
                RegexOptions.Multiline);

            string payerNotes = messageMatch.Success
                ? messageMatch.Groups[1].Value.Trim()
                : "";

            var refMatch = Regex.Match(
                text,
                @"Reference Number:\s*(\S+)",
                RegexOptions.Multiline);

            string reference = refMatch.Success
                ? refMatch.Groups[1].Value.Trim()
                : "";

            var transactionLogId = await _updateBalanceSvc.AddToBalanceAsync(
                balance!.Id,
                unitsToAdd,
                amount,
                "Handled automatically",
                currency,
                payerNotes,
                reference,
                "INTERAC");

            if (transactionLogId.HasValue)
            {
                await _logHelper.LogEmailPaymentsAsync(
                    WasHandledAutomatically: true,
                    StudentBalanceTransactionLogId: transactionLogId.Value,
                    StudentName: student.Name,
                    PayerName: sentFrom,
                    Subject: message.Subject);
            }

        }
    }
}
