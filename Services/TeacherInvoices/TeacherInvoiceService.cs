using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public sealed class TeacherInvoiceService(MyDatabaseContext db, TorontoInvoiceClock clock,
    InvoiceHtmlRenderer renderer, DocuSealClient docuseal, IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("TeacherInvoices.Ticket.v1");
    public async Task<TeacherInvoiceInput> DefaultsAsync(Guid teacherId, CancellationToken ct)
    {
        var t = await db.Teacher.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teacherId, ct)
            ?? throw new InvoiceValidationException("Teacher profile not found.");
        var (start, end) = clock.LastWeek();
        return new TeacherInvoiceInput {
            StartDate = start, EndDate = end,
            InvoiceNumber = $"INV-{clock.Today:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}",
            Name = t.Name, Email = t.Email ?? "", StreetAddress = t.StreetAddress,
            City = t.City, ProvinceOrState = t.ProvinceOrState, PostalCode = t.PostalCode,
            Country = t.Country, PhoneNumber = t.PhoneNumber, ETransferEmail = t.ETransferEmail ?? t.Email
        };
    }
    public async Task<TeacherInvoiceSnapshot> BuildAsync(Guid teacherId, TeacherInvoiceInput input, CancellationToken ct)
    {
        input.StartDate = input.StartDate.Date; input.EndDate = input.EndDate.Date;
        if (input.StartDate == default || input.EndDate == default || input.StartDate > input.EndDate)
            throw new InvoiceValidationException("Choose a valid date range with the start on or before the end.");
        if (input.EndDate >= DateTime.MaxValue.Date)
            throw new InvoiceValidationException("The end date is outside the supported range.");
        if (!await db.Teacher.AnyAsync(t => t.Id == teacherId, ct))
            throw new InvoiceValidationException("Teacher profile not found.");
        var from = clock.ToUtc(input.StartDate);
        var until = clock.ToUtc(input.EndDate.AddDays(1));
        // Same eligibility as LessonHistory. No Accounted/minimum-duration/multiplier filter.
        input.CorrectionDescription = input.CorrectionDescription?.Trim();
        input.CorrectionDate = input.CorrectionDate?.Date;

        input.Note = string.IsNullOrWhiteSpace(input.Note)
            ? null
            : input.Note.Trim();

        /*if (input.CorrectionAmount.HasValue)
        {
            input.CorrectionAmount = decimal.Round(
                input.CorrectionAmount.Value,
                2,
                MidpointRounding.AwayFromZero);
        }*/
        var lessons = await db.Schedule.AsNoTracking().Include(s => s.Group)
            .Where(s => s.TeacherId == teacherId && s.Status == "Taken" && s.DateTime >= from && s.DateTime < until)
            .OrderBy(s => s.DateTime).ThenBy(s => s.Id).ToListAsync(ct);

        if (lessons.Count == 0) throw new InvoiceValidationException("No Taken lessons were found for this date range.");

        var groupIds = lessons
            .Select(s => s.GroupId)
            .Distinct()
            .ToList();

        var memberships = await db.Set<StudentGroupComposition>()
            .AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId))
            .Select(m => new { m.GroupId, m.StudentId })
            .ToListAsync(ct);

        var studentsByGroup = memberships
            .GroupBy(m => m.GroupId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(m => m.StudentId).Distinct().ToArray());

        var soloStudentIds = studentsByGroup.Values
            .Where(ids => ids.Length == 1)
            .Select(ids => ids[0])
            .Distinct()
            .ToList();

        var usernames = await db.User
            .AsNoTracking()
            .Where(u => soloStudentIds.Contains(u.ID))
            .ToDictionaryAsync(u => u.ID, u => u.Username, ct);

        string GetLessonLabel(Schedule schedule)
        {
            if (!studentsByGroup.TryGetValue(schedule.GroupId, out var studentIds) ||
                studentIds.Length == 0)
            {
                throw new InvoiceValidationException(
                    $"No students were found for lesson {schedule.Id}.");
            }

            if (studentIds.Length > 1)
                return "a group";

            if (!usernames.TryGetValue(studentIds[0], out var username) ||
                string.IsNullOrWhiteSpace(username))
            {
                throw new InvoiceValidationException(
                    $"No student username was found for lesson {schedule.Id}.");
            }

            return username;
        }

        if (lessons.Any(s => string.IsNullOrWhiteSpace(s.TeacherPayCurrency)))
            throw new InvoiceValidationException("At least one included lesson has no teacher pay currency. Fix the schedule before invoicing.");

        var currencies = lessons.Select(s => s.TeacherPayCurrency.Trim().ToUpperInvariant()).Distinct().ToArray();

        if (currencies.Length != 1)
            throw new InvoiceValidationException("The included lessons have multiple teacher pay currencies: " + string.Join(", ", currencies) + ". Choose a single-currency date range.");
        
        if (lessons.Any(s => !float.IsFinite(s.TeacherPayAmount)))
            throw new InvoiceValidationException("At least one included lesson has an invalid teacher pay amount.");
        
        var today = clock.Today;
        List<InvoiceLesson> lines;
        try
        {
            lines = lessons.Select(s => new InvoiceLesson(s.Id, clock.FromUtc(s.DateTime),
                GetLessonLabel(s), s.Duration,
                decimal.Round((decimal)s.TeacherPayAmount, 2, MidpointRounding.AwayFromZero))).ToList();
            //_ = lines.Sum(x => x.Amount) + (input.CorrectionAmount ?? 0m);
            _ = lines.Sum(x => x.Amount);
        }
        catch (OverflowException)
        {
            throw new InvoiceValidationException("A teacher pay amount or invoice total is outside the supported numeric range.");
        }
        return new TeacherInvoiceSnapshot {
            TeacherId = teacherId, Input = input, InvoiceDate = today,
            PaymentDue = today.AddDays(7), Currency = currencies[0],
            Lessons = lines
        };
    }
    public async Task<InvoiceSigningSession> BeginAsync(TeacherInvoiceSnapshot invoice, CancellationToken ct)
    {
        var (id, url) = await docuseal.CreateAsync(invoice, renderer.Render(invoice), ct);
        // Holds the exact generated snapshot; no DB table, cache, or session is required.
        var token = protector.Protect(JsonSerializer.Serialize(new InvoiceTicket {
            SubmissionId = id, Invoice = invoice, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        }));
        return new InvoiceSigningSession(id, url, token);
    }
    public async Task<SignedTeacherInvoice> GetSignedAsync(Guid teacherId, string token, CancellationToken ct)
    {
        InvoiceTicket ticket;
        try { ticket = JsonSerializer.Deserialize<InvoiceTicket>(protector.Unprotect(token))
            ?? throw new InvalidOperationException(); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or InvalidOperationException or ArgumentException)
        { throw new InvoiceValidationException("This invoice session is invalid. Generate a new invoice."); }
        if (ticket.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvoiceValidationException("This invoice session has expired. The signed document remains in DocuSeal.");
        if (ticket.Invoice.TeacherId != teacherId) throw new UnauthorizedAccessException();
        var (pdf, completed, audit) = await docuseal.GetSignedAsync(ticket.SubmissionId, teacherId, ct);
        var safeName = Regex.Replace(ticket.Invoice.Input.InvoiceNumber, @"[^A-Za-z0-9._-]", "_").Trim('.');
        if (safeName.Length == 0) safeName = "invoice";
        return new SignedTeacherInvoice(ticket.SubmissionId, ticket.Invoice, pdf, safeName + ".pdf", completed, audit);
    }
}
