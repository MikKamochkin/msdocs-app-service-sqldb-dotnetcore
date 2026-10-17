using Microsoft.Extensions.DependencyInjection;

namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public static class TeacherInvoiceRegistration
{
    public static IServiceCollection AddTeacherInvoices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TeacherInvoiceOptions>(configuration.GetSection("TeacherInvoices"));
        services.AddDataProtection();
        services.AddSingleton<TorontoInvoiceClock>();
        services.AddScoped<InvoiceHtmlRenderer>();
        services.AddScoped<TeacherInvoiceService>();
        services.AddScoped<ISignedInvoiceHandler, SignedInvoiceHandler>();
        services.AddHttpClient<DocuSealClient>((sp, client) => {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeacherInvoiceOptions>>().Value;
            var uri = new Uri(o.ApiBaseUrl.TrimEnd('/') + "/");
            if (uri.Scheme != "https") throw new InvalidOperationException("ApiBaseUrl must use HTTPS.");
            client.BaseAddress = uri; client.Timeout = TimeSpan.FromSeconds(90);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient("DocuSealFiles", c => c.Timeout = TimeSpan.FromSeconds(90))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        return services;
    }
}
