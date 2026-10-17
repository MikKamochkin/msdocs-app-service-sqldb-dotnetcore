using DotNetCoreSqlDb.Models;
using DotNetCoreSqlDb.Services.TeacherInvoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCoreSqlDb.Controllers;

[Authorize(Roles = "teacher")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TeacherInvoicesController(TeacherInvoiceService invoices,
    ISignedInvoiceHandler handler, ILogger<TeacherInvoicesController> logger) : Controller
{
    private bool TryTeacher(out Guid id) => Guid.TryParse(User.FindFirst("UserID")?.Value, out id);
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!TryTeacher(out var id)) return Forbid();
        try { return View(new TeacherInvoicePage { Input = await invoices.DefaultsAsync(id, ct) }); }
        catch (InvoiceValidationException ex) { return NotFound(ex.Message); }
    }
    [HttpPost]
    public async Task<IActionResult> Preview([Bind(Prefix = "Input")] TeacherInvoiceInput input, CancellationToken ct)
        => await BuildPage(input, false, ct);
    [HttpPost]
    public async Task<IActionResult> Generate([Bind(Prefix = "Input")] TeacherInvoiceInput input, CancellationToken ct)
        => await BuildPage(input, true, ct);
    private async Task<IActionResult> BuildPage(TeacherInvoiceInput input, bool sign, CancellationToken ct)
    {
        if (!TryTeacher(out var id)) return Forbid();
        var page = new TeacherInvoicePage { Input = input };
        if (!ModelState.IsValid) return View("Index", page);
        try
        {
            page.Preview = await invoices.BuildAsync(id, input, ct);
            if (sign)
            {
                var session = await invoices.BeginAsync(page.Preview, ct);
                page.SigningUrl = session.SigningUrl; page.Token = session.Token;
            }
        }
        catch (InvoiceValidationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("DocuSeal invoice generation failed ({ErrorType}).", ex.GetType().Name);
            ModelState.AddModelError("", "DocuSeal could not generate the invoice. Check configuration or try again. If a request timed out, check DocuSeal before retrying.");
        }
        return View("Index", page);
    }
    [HttpPost]
    public async Task<IActionResult> Complete(string token, CancellationToken ct)
    {
        if (!TryTeacher(out var id)) return Forbid();
        try
        {
            var signedInvoice = await invoices.GetSignedAsync(id, token, ct);
            await handler.HandleAsync(signedInvoice, ct);
            return Json(new { ready = true, submissionId = signedInvoice.SubmissionId });
        }
        catch (InvoiceNotCompletedException) { return StatusCode(202, new { ready = false }); }
        catch (InvoiceValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError("Signed invoice processing failed ({ErrorType}).", ex.GetType().Name);
            return StatusCode(502, new { message = "The signed invoice could not be processed. Try checking again; if this persists, contact the administrator." });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Pdf(string token, bool download, CancellationToken ct)
    {
        if (!TryTeacher(out var id)) return Forbid();
        try
        {
            var invoice = await invoices.GetSignedAsync(id, token, ct);
            if (download) return File(invoice.PdfBytes, "application/pdf", invoice.FileName);
            // Open in the browser PDF viewer for printing. Token is sent in POST body.
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{invoice.FileName}\"";
            return File(invoice.PdfBytes, "application/pdf");
        }
        catch (InvoiceNotCompletedException) { return Conflict("The signed PDF is not ready yet. Check completion and try again."); }
        catch (InvoiceValidationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Signed PDF retrieval failed ({ErrorType}).", ex.GetType().Name);
            return StatusCode(502, "Could not retrieve the signed PDF. Check AllowedDownloadHosts and try again.");
        }
    }
}
