using Microsoft.AspNetCore.Authentication.Cookies;
using Monitor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<AdminCredentialOptions>(
    builder.Configuration.GetSection(AdminCredentialOptions.SectionName));
builder.Services.AddSingleton<IAdminCredentialVerifier, AdminCredentialVerifier>();
builder.Services.AddSingleton<ILoginAttemptLimiter, LoginAttemptLimiter>();
builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();
builder.Services.AddSingleton<IDemoMonitorService, DemoMonitorService>();

var registrationStoreOptions = builder.Configuration
    .GetSection(RegistrationStoreOptions.SectionName)
    .Get<RegistrationStoreOptions>() ?? new RegistrationStoreOptions();
registrationStoreOptions.Validate();
builder.Services.AddSingleton(registrationStoreOptions);
builder.Services.AddSingleton<IServerRegistrationRepository>(_ =>
{
    if (registrationStoreOptions.Mode == RegistrationStoreMode.InMemory)
    {
        return new InMemoryServerRegistrationRepository();
    }

    var storePath = Path.IsPathRooted(registrationStoreOptions.Path)
        ? Path.GetFullPath(registrationStoreOptions.Path)
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, registrationStoreOptions.Path));
    var webRoot = Path.GetFullPath(builder.Environment.WebRootPath
        ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
    var relativeToWebRoot = Path.GetRelativePath(webRoot, storePath);
    if (relativeToWebRoot == "." ||
        (!relativeToWebRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
         !string.Equals(relativeToWebRoot, "..", StringComparison.Ordinal) &&
         !Path.IsPathRooted(relativeToWebRoot)))
    {
        throw new InvalidOperationException("RegistrationStore:Path must be outside wwwroot.");
    }

    return new FileServerRegistrationRepository(storePath);
});

builder.Services.AddSingleton<IExternalConnectionSecretProvider, EnvironmentConnectionSecretProvider>();
builder.Services.AddSingleton<IConnectionSecretStore, ConfigurationConnectionSecretStore>();
builder.Services.AddSingleton<IRuntimeCredentialWriter>(provider => (IRuntimeCredentialWriter)provider.GetRequiredService<IConnectionSecretStore>());
builder.Services.AddSingleton<ISqlConnectionProbe, SqlConnectionProbe>();
builder.Services.AddSingleton<IServerConnectionTester, ServerConnectionTester>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISqlSnapshotQuery, SqlSnapshotQuery>();
builder.Services.AddSingleton<ISqlServerSnapshotCollector, SqlServerSnapshotCollector>();
builder.Services.AddSingleton<IServerHealthSnapshotCache, ServerHealthSnapshotCache>();
builder.Services.AddSingleton<IHealthRuleEvaluator, HealthRuleEvaluator>();
builder.Services.AddSingleton<IHealthIncidentRepository, InMemoryHealthIncidentRepository>();
builder.Services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddSingleton<IAdvisorContextBuilder, AdvisorContextBuilder>();
builder.Services.AddSingleton<IAdvisorProvider, DisabledAdvisorProvider>();
builder.Services.AddSingleton<IIncidentWorkflowService, IncidentWorkflowService>();
builder.Services.AddSingleton<IAdvisorRequestService, AdvisorRequestService>();
builder.Services.AddSingleton<ISnapshotHistoryStore, InMemorySnapshotHistoryStore>();
builder.Services.AddSingleton<ISnapshotObserver, SnapshotObserver>();
builder.Services.AddSingleton<ISnapshotCollectionCycle, SnapshotCollectionCycle>();
builder.Services.AddSingleton<ITrendReadService, TrendReadService>();
var scheduleOptions = builder.Configuration.GetSection(SnapshotScheduleOptions.SectionName).Get<SnapshotScheduleOptions>() ?? new();
scheduleOptions.Validate();
builder.Services.AddSingleton(scheduleOptions);
builder.Services.AddSingleton<ICollectionBackoffPolicy, CollectionBackoffPolicy>();
builder.Services.AddSingleton<ISchedulerStatusStore, SchedulerStatusStore>();
builder.Services.AddHostedService<SnapshotSchedulerService>();
builder.Services.AddSingleton<IMonitorReadService, MonitorReadService>();
builder.Services.AddSingleton<ISnapshotRefreshService, SnapshotRefreshService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "Monitor.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(MonitorPolicies.Read, policy => policy.RequireRole(MonitorRoles.Viewer, MonitorRoles.Operator, MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Operate, policy => policy.RequireRole(MonitorRoles.Operator, MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Manage, policy => policy.RequireRole(MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Advisor, policy => policy.RequireRole(MonitorRoles.Operator, MonitorRoles.Administrator));
});

var app = builder.Build();

var configuredRegistration = ConfiguredServerRegistrationLoader.Load(
    app.Configuration,
    app.Services.GetRequiredService<TimeProvider>());
if (configuredRegistration is not null)
{
    app.Services.GetRequiredService<IServerRegistrationRepository>().Upsert(configuredRegistration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
