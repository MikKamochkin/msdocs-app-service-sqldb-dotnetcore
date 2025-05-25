using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Azure.Identity;
using DotNetCoreSqlDb.Services;
using DotNetCoreSqlDb.Settings;  // ← NEW
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using System.Net.Http.Headers;
using DotNetCoreSqlDb.Hubs;

var builder = WebApplication.CreateBuilder(args);

/* ───── 1.  Load Azure Key Vault (already present) ───── */
var vaultUri = new Uri("https://tslvault.vault.azure.net/");

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
{
    // only in non-CI environments
    builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
}

/* ───── 2.  Strongly-typed Email settings + service ──── */
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddTransient<IEmailSender, MailKitEmailSender>();

/* ───── 3.  Database context + cache (unchanged) ─────── */
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("MyDbConnection")));
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING")));
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["AZURE_REDIS_CONNECTIONSTRING"];
        options.InstanceName = "SampleInstance";
    });
}

/* ───── 4.  Wassenger (existing) ─────────────────────── */
var WassengerApiKey         = builder.Configuration["WASSENGER_API_KEY"];
var WassengerExpectedSecret = builder.Configuration["WASSENGER_WEBHOOK_EXPECTED_SECRET"];
var ZoomClientId = builder.Configuration["ZoomClientId"];
var ZoomClientSecret = builder.Configuration["ZoomClientSecret"];
var ZoomAccountId = builder.Configuration["ZoomAccountId"];

/* ───── 5.  MVC / auth / SignalR / session (unchanged) ─ */
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath          = "/Login/Index";
        options.LogoutPath         = "/Login/Logout";
        options.AccessDeniedPath   = "/Home/AccessDenied";
        options.ExpireTimeSpan     = TimeSpan.FromHours(24);
        options.SlidingExpiration  = true;
    });

builder.Services.AddSignalR();

builder.Services.AddHttpClient("Wassenger", client =>
{
    client.BaseAddress = new Uri("https://api.wassenger.com");
    client.DefaultRequestHeaders.Add("Token", builder.Configuration["WASSENGER-API-KEY"]);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["WASSENGER-API-KEY"]);
});
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IZoomApiService, ZoomApiService>();
builder.Services.AddScoped<IZoomMeetingService, ZoomMeetingService>();

builder.Services.AddHostedService<TimedHostedService>();

builder.Logging.AddAzureWebAppDiagnostics();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

/* ───── 6.  Pipeline (unchanged) ────────────────────── */
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WhatsAppHub>("/hubs/whstatus");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
