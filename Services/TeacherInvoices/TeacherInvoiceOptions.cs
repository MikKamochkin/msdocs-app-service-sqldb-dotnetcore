namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public sealed class TeacherInvoiceOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.docuseal.com/";
    public string ApiKey { get; set; } = "";
    public string SigningOrigin { get; set; } = "https://docuseal.com";
    // Only backend-controlled DocuSeal download hosts. Add a host if your account
    // returns a different CDN host. Never populate this from browser input.
    public string[] AllowedDownloadHosts { get; set; } = ["docuseal.com", "www.docuseal.com"];
    public string CompanyName { get; set; } = "torontofrench.com Inc.";
    public string CompanyStreet { get; set; } = "182 Cabernet Rd";
    public string CompanyCityLine { get; set; } = "Thornhill, ON, L4J 8V8";
    public string CompanyEmail { get; set; } = "info@torontofrench.com";
    public string ServiceDescription { get; set; } = "French tutoring";
}
