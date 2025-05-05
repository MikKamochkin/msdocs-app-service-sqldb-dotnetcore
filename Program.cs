using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using DotNetCoreSqlDb.Services;
using System.Net.Http.Headers;
using DotNetCoreSqlDb.Hubs;

var builder = WebApplication.CreateBuilder(args);

/*builder.Configuration.AddAzureKeyVault(
        new Uri("https://tslvault.vault.azure.net/"),
        new DefaultAzureCredential());*/

var vaultUri = new Uri("https://tslvault.vault.azure.net/");

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
{
    // only in non‑CI environments
    builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
}

// Add database context and cache
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

// Program.cs (anywhere you already have builder)

var WassengerApiKey = builder.Configuration["WASSENGER_API_KEY"];

var WassengerExpectedSecret = builder.Configuration["WASSENGER_WEBHOOK_EXPECTED_SECRET"];


// Add services to the container.
builder.Services.AddControllersWithViews();

// Add cookie authentication services.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";       // Redirect here if not authenticated.
        options.LogoutPath = "/Login/Logout";       // Path to logout.
        options.AccessDeniedPath = "/Home/AccessDenied"; // Optional: path for denied access.
        options.ExpireTimeSpan = TimeSpan.FromHours(24); // Cookie expiration time.
        options.SlidingExpiration = true; // After every valid request the cookie's lifetime is "slid" forward, once every 12 hours
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

// Add App Service logging
builder.Logging.AddAzureWebAppDiagnostics();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// IMPORTANT: Add authentication middleware before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WhatsAppHub>("/hubs/whstatus");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();