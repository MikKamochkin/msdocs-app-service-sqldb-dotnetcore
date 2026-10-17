using System.Net.Http.Json;
using System.Text.Json;
using DotNetCoreSqlDb.Models;
using Microsoft.Extensions.Options;

namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public sealed class DocuSealClient(HttpClient http, IHttpClientFactory factory,
    IOptions<TeacherInvoiceOptions> options)
{
    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
            throw new InvoiceValidationException("DocuSeal is not configured. Ask the administrator to configure the API key.");
        request.Headers.Add("X-Auth-Token", o.ApiKey);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            // Do not expose or log response bodies containing signer information.
            throw new HttpRequestException($"DocuSeal returned HTTP {(int)response.StatusCode}.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<(long Id, string Url)> CreateAsync(TeacherInvoiceSnapshot invoice,
        string html, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "submissions/html")
        {
            Content = JsonContent.Create(new
            {
                name = $"Teacher invoice {invoice.Input.InvoiceNumber}", send_email = false,
                documents = new[] { new { name = "Teacher invoice", html, size = "Letter" } },
                submitters = new[] { new {
                    role = "Teacher", name = invoice.Input.Name, email = invoice.Input.Email,
                    external_id = invoice.TeacherId.ToString("D"), send_email = false,
                    metadata = new { invoice_number = invoice.Input.InvoiceNumber }
                } }
            })
        };
        using var doc = await SendAsync(request, ct);
        var root = doc.RootElement;
        var signer = root.GetProperty("submitters").EnumerateArray().Single();
        var slug = signer.GetProperty("slug").GetString();
        if (string.IsNullOrWhiteSpace(slug)) throw new HttpRequestException("DocuSeal returned no signer URL.");
        var origin = new Uri(options.Value.SigningOrigin);
        if (origin.Scheme != "https") throw new InvalidOperationException("SigningOrigin must use HTTPS.");
        return (root.GetProperty("id").GetInt64(), new Uri(origin, "/s/" + Uri.EscapeDataString(slug)).AbsoluteUri);
    }
    public async Task<(byte[] Pdf, DateTimeOffset CompletedAt, string? AuditUrl)> GetSignedAsync(
        long id, Guid teacherId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"submissions/{id}");
        using var doc = await SendAsync(request, ct);
        var root = doc.RootElement;
        var signers = root.GetProperty("submitters").EnumerateArray().ToArray();
        if (signers.Length != 1 ||
            signers[0].GetProperty("external_id").GetString() != teacherId.ToString("D") ||
            signers[0].GetProperty("role").GetString() != "Teacher")
            throw new UnauthorizedAccessException("The submission does not belong to this teacher.");
        if (root.GetProperty("status").GetString() != "completed" ||
            signers[0].GetProperty("status").GetString() != "completed")
            throw new InvoiceNotCompletedException();
        var completedAt = root.GetProperty("completed_at").GetDateTimeOffset();
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        if (documents.Length != 1) throw new HttpRequestException("Expected one signed invoice document.");
        var url = documents[0].GetProperty("url").GetString();
        var pdf = await DownloadAsync(url, ct);
        string? audit = root.TryGetProperty("audit_log_url", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
        return (pdf, completedAt, audit);
    }
    private bool Allowed(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort &&
        options.Value.AllowedDownloadHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
    private async Task<byte[]> DownloadAsync(string? url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !Allowed(uri))
            throw new HttpRequestException("DocuSeal returned a download host that is not allowed. Check AllowedDownloadHosts.");
        // Separate client: NEVER send the API key to file/CDN hosts. Redirects are checked manually.
        using var files = factory.CreateClient("DocuSealFiles");
        for (int redirect = 0; redirect < 5; redirect++)
        {
            using var response = await files.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location ?? throw new HttpRequestException("Missing redirect URL.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (!Allowed(uri)) throw new HttpRequestException("Download redirect host is not allowed. Check AllowedDownloadHosts.");
                continue;
            }
            response.EnsureSuccessStatusCode();
            const int limit = 25 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new HttpRequestException("Signed PDF exceeds 25 MB.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var output = new MemoryStream();
            var buffer = new byte[81920]; int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                if (output.Length + read > limit) throw new HttpRequestException("Signed PDF exceeds 25 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            var bytes = output.ToArray();
            if (bytes.Length < 5 || System.Text.Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
                throw new HttpRequestException("DocuSeal did not return a PDF.");
            return bytes;
        }
        throw new HttpRequestException("Too many signed PDF redirects.");
    }
}
