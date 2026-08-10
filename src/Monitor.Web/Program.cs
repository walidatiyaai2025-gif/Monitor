using Microsoft.AspNetCore.Authentication.Cookies;
using Monitor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<AdminCredentialOptions>(
    builder.Configuration.GetSection(AdminCredentialOptions.SectionName));
builder.Services.AddSingleton<IAdminCredentialVerifier, AdminCredentialVerifier>();
builder.Services.AddSingleton<IDemoMonitorService, DemoMonitorService>();
builder.Services.AddSingleton<IServerRegistrationRepository, InMemoryServerRegistrationRepository>();
builder.Services.AddSingleton<IConnectionSecretStore, ConfigurationConnectionSecretStore>();
builder.Services.AddSingleton<IConnectionProfileFactory, SqlConnectionProfileFactory>();
builder.Services.AddSingleton<ISqlConnectionProbe, SqlClientConnectionProbe>();
builder.Services.AddSingleton<ISqlConnectionTester, SqlConnectionTester>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "Monitor.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
