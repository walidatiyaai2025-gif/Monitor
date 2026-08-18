using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Monitor.Web.Services;

var builder = WebApplication.CreateBuilder(args);
ProductionAdminCredentialGuard.Validate(builder.Environment);
DemoDataEnvironmentGuard.Validate(builder.Environment, builder.Configuration);
builder.Host.UseWindowsService(options => options.ServiceName = "Monitor");

builder.Services.AddControllersWithViews();
builder.Services.Configure<AdminCredentialOptions>(builder.Configuration.GetSection(AdminCredentialOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAdminCredentialVerifier, AdminCredentialVerifier>();
builder.Services.AddSingleton<ILoginAttemptLimiter, LoginAttemptLimiter>();
builder.Services.AddSingleton<IDemoMonitorService, DemoMonitorService>();
builder.Services.AddSingleton<IMonitorTelemetry, MonitorTelemetry>();

var webSecurityOptions = builder.Configuration.GetSection(WebSecurityOptions.SectionName).Get<WebSecurityOptions>() ?? new();
webSecurityOptions.Validate();
builder.Services.AddSingleton(webSecurityOptions);
builder.Services.AddSingleton<AbsoluteSessionCookieEvents>();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(webSecurityOptions.HstsDays);
    options.IncludeSubDomains = webSecurityOptions.HstsIncludeSubDomains;
    options.Preload = webSecurityOptions.HstsPreload;
});
if (webSecurityOptions.HasTrustedForwarders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options => TrustedForwarderPolicy.Configure(options, webSecurityOptions));
}

var performanceOptions = builder.Configuration.GetSection(PerformanceScaleOptions.SectionName).Get<PerformanceScaleOptions>() ?? new();
performanceOptions.Validate();
builder.Services.AddSingleton(performanceOptions);
builder.Services.AddSingleton<ManualRefreshConcurrencyGate>();

var deploymentTopologyOptions = builder.Configuration.GetSection(DeploymentTopologyOptions.SectionName).Get<DeploymentTopologyOptions>() ?? new();
deploymentTopologyOptions.Validate();
builder.Services.AddSingleton(deploymentTopologyOptions);

var sharedStateOptions = builder.Configuration.GetSection(SharedStateOptions.SectionName).Get<SharedStateOptions>() ?? new();
sharedStateOptions.Validate();
builder.Services.AddSingleton(sharedStateOptions);
builder.Services.AddSingleton<ISharedStateDocumentStore>(_ => sharedStateOptions.Provider == SharedStateProviderKind.SqlServer ? new SqlServerSharedStateDocumentStore(sharedStateOptions) : new DisabledSharedStateDocumentStore());
builder.Services.AddSingleton<ISharedStateReadinessService, SharedStateReadinessService>();

var haStateOptions = builder.Configuration.GetSection(HaStateOptions.SectionName).Get<HaStateOptions>() ?? new();
haStateOptions.Validate();
builder.Services.AddSingleton(haStateOptions);

var coordinationOptions = builder.Configuration.GetSection(DistributedCoordinationOptions.SectionName).Get<DistributedCoordinationOptions>() ?? new();
coordinationOptions.Validate();
builder.Services.AddSingleton(coordinationOptions);

var keyStoreOptions = builder.Configuration.GetSection(DataProtectionKeyStoreOptions.SectionName).Get<DataProtectionKeyStoreOptions>() ?? new();
keyStoreOptions.Validate();
builder.Services.AddSingleton(keyStoreOptions);
var credentialPolicy = builder.Configuration.GetSection(CredentialPolicyOptions.SectionName).Get<CredentialPolicyOptions>() ?? new();
builder.Services.AddSingleton(credentialPolicy);

if ((haStateOptions.UseSharedRegistrations || haStateOptions.UseSharedOperationalState || coordinationOptions.Enabled || keyStoreOptions.Mode == DataProtectionKeyStoreMode.SharedState) && sharedStateOptions.Provider != SharedStateProviderKind.SqlServer)
    throw new InvalidOperationException("Shared application state, coordination and shared key management require the dedicated Monitor shared-state SQL provider.");

var nodeIdentity = NodeIdentity.Resolve(coordinationOptions);
builder.Services.AddSingleton(nodeIdentity);
builder.Services.AddSingleton<IDistributedLeaseManager>(provider => coordinationOptions.Enabled
    ? new SharedStateDistributedLeaseManager(provider.GetRequiredService<ISharedStateDocumentStore>(), nodeIdentity, provider.GetRequiredService<TimeProvider>(), coordinationOptions)
    : new DisabledDistributedLeaseManager());

var registrationStoreOptions = builder.Configuration.GetSection(RegistrationStoreOptions.SectionName).Get<RegistrationStoreOptions>() ?? new();
registrationStoreOptions.Validate();
builder.Services.AddSingleton(registrationStoreOptions);

string ResolveRegistrationStorePath()
{
    var storePath = Path.IsPathRooted(registrationStoreOptions.Path) ? Path.GetFullPath(registrationStoreOptions.Path) : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, registrationStoreOptions.Path));
    var webRoot = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
    var relativeToWebRoot = Path.GetRelativePath(webRoot, storePath);
    if (relativeToWebRoot == "." || (!relativeToWebRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !string.Equals(relativeToWebRoot, "..", StringComparison.Ordinal) && !Path.IsPathRooted(relativeToWebRoot)))
        throw new InvalidOperationException("RegistrationStore:Path must be outside wwwroot.");
    return storePath;
}

builder.Services.AddSingleton<IServerRegistrationRepository>(provider =>
{
    if (haStateOptions.UseSharedRegistrations)
    {
        var shared = new AtomicSharedServerRegistrationRepository(provider.GetRequiredService<ISharedStateDocumentStore>());
        if (haStateOptions.ImportLocalRegistrationsWhenSharedEmpty)
        {
            var legacyPath = ResolveRegistrationStorePath();
            if (File.Exists(legacyPath)) shared.ImportIfEmpty(new FileServerRegistrationRepository(legacyPath).GetAll());
        }
        return shared;
    }
    return registrationStoreOptions.Mode == RegistrationStoreMode.InMemory ? new InMemoryServerRegistrationRepository() : new FileServerRegistrationRepository(ResolveRegistrationStorePath());
});
builder.Services.AddSingleton<ServerRegistrationMutationGate>();

var operationalStoreOptions = builder.Configuration.GetSection(OperationalStoreOptions.SectionName).Get<OperationalStoreOptions>() ?? new();
operationalStoreOptions.Validate();
builder.Services.AddSingleton(operationalStoreOptions);
var operationalRoot = !haStateOptions.UseSharedOperationalState && operationalStoreOptions.Mode == OperationalStoreMode.File
    ? OperationalStorePath.ResolveOutsideWebRoot(operationalStoreOptions.RootPath, builder.Environment.ContentRootPath, builder.Environment.WebRootPath)
    : null;

builder.Services.AddSingleton<IAuditStore>(provider =>
{
    IAuditStore inner = haStateOptions.UseSharedOperationalState
        ? new SharedAuditStore(provider.GetRequiredService<ISharedStateDocumentStore>(), provider.GetRequiredService<TimeProvider>())
        : operationalRoot is null
            ? new InMemoryAuditStore(provider.GetRequiredService<TimeProvider>())
            : new FileAuditStore(Path.Combine(operationalRoot, "audit.json"), provider.GetRequiredService<TimeProvider>());
    var bounded = new PerformanceBoundedAuditStore(inner, performanceOptions);
    return new CoordinatedIncidentNoteAuditStore(
        bounded,
        provider.GetRequiredService<ISharedStateDocumentStore>(),
        provider.GetRequiredService<TimeProvider>(),
        haStateOptions.UseSharedOperationalState);
});
builder.Services.AddSingleton<IHealthIncidentRepository>(provider =>
{
    IHealthIncidentRepository inner = haStateOptions.UseSharedOperationalState
        ? new SharedHealthIncidentRepository(provider.GetRequiredService<ISharedStateDocumentStore>())
        : operationalRoot is null
            ? new InMemoryHealthIncidentRepository()
            : new FileHealthIncidentRepository(Path.Combine(operationalRoot, "incidents.json"));
    return new TelemetryHealthIncidentRepository(inner, provider.GetRequiredService<IMonitorTelemetry>());
});
builder.Services.AddSingleton<ISnapshotHistoryStore>(provider => haStateOptions.UseSharedOperationalState
    ? new SharedSnapshotHistoryStore(provider.GetRequiredService<ISharedStateDocumentStore>(), provider.GetRequiredService<TimeProvider>())
    : operationalRoot is null ? new InMemorySnapshotHistoryStore(provider.GetRequiredService<TimeProvider>()) : new FileSnapshotHistoryStore(Path.Combine(operationalRoot, "history.json"), provider.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IOperatorMetadataStore>(provider => haStateOptions.UseSharedOperationalState
    ? new SharedOperatorMetadataStore(provider.GetRequiredService<ISharedStateDocumentStore>(), provider.GetRequiredService<TimeProvider>())
    : operationalRoot is null
        ? new InMemoryOperatorMetadataStore(provider.GetRequiredService<TimeProvider>())
        : new FileOperatorMetadataStore(Path.Combine(operationalRoot, "operator-metadata.json"), provider.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ISafeCsvReportService, SafeCsvReportService>();
builder.Services.AddSingleton<IRedactedDiagnosticsPackageService, RedactedDiagnosticsPackageService>();

var secretStoreOptions = builder.Configuration.GetSection(SecretStoreOptions.SectionName).Get<SecretStoreOptions>() ?? new();
var secretFilePath = OperationalStorePath.ResolveOutsideWebRoot(secretStoreOptions.Path, builder.Environment.ContentRootPath, builder.Environment.WebRootPath);
var keyRingPath = OperationalStorePath.ResolveOutsideWebRoot(secretStoreOptions.KeyRingPath, builder.Environment.ContentRootPath, builder.Environment.WebRootPath);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Monitor.SqlSecrets.v1");
if (keyStoreOptions.Mode == DataProtectionKeyStoreMode.SharedState)
{
    var kek = Environment.GetEnvironmentVariable(keyStoreOptions.KeyEncryptionKeyEnvironmentVariable);
    if (string.IsNullOrWhiteSpace(kek)) throw new InvalidOperationException("Shared Data Protection key encryption key is unavailable.");
    var sharedKeyRepository = new SharedEncryptedDataProtectionXmlRepository(new SqlServerSharedStateDocumentStore(sharedStateOptions), kek);
    dataProtection.AddKeyManagementOptions(options => options.XmlRepository = sharedKeyRepository);
}
else
{
    Directory.CreateDirectory(keyRingPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

builder.Services.AddSingleton(secretStoreOptions);
builder.Services.AddSingleton<IExternalConnectionSecretProvider, EnvironmentConnectionSecretProvider>();
builder.Services.AddSingleton<IConnectionSecretStore>(provider => new ProtectedFileConnectionSecretStore(secretFilePath, provider.GetRequiredService<IDataProtectionProvider>(), provider.GetRequiredService<IConfiguration>(), provider.GetServices<IExternalConnectionSecretProvider>(), credentialPolicy));
builder.Services.AddSingleton<IRuntimeCredentialWriter>(provider => (IRuntimeCredentialWriter)provider.GetRequiredService<IConnectionSecretStore>());
builder.Services.AddSingleton<ISqlConnectionProbe, SqlConnectionProbe>();
builder.Services.AddSingleton<IServerConnectionTester, ServerConnectionTester>();
builder.Services.AddSingleton<AtomicCredentialLifecycleService>();
builder.Services.AddSingleton<ICredentialLifecycleService>(provider => new WriteAheadAuditedCredentialLifecycleService(
    provider.GetRequiredService<AtomicCredentialLifecycleService>(),
    provider.GetRequiredService<ServerRegistrationMutationGate>(),
    provider.GetRequiredService<IAuditStore>()));
builder.Services.AddSingleton<ICredentialReadinessService, CredentialReadinessService>();
builder.Services.AddSingleton<IServerTargetLifecycleService>(provider => new ServerTargetLifecycleService(
    provider.GetRequiredService<IServerRegistrationRepository>(),
    provider.GetRequiredService<IServerHealthSnapshotCache>(),
    provider.GetRequiredService<ServerRegistrationMutationGate>(),
    provider.GetRequiredService<IAuditStore>()));
builder.Services.AddSingleton<ISqlSnapshotQuery, GovernedSqlSnapshotQuery>();
builder.Services.AddSingleton<SqlServerSnapshotCollector>();
builder.Services.AddSingleton<ISqlServerSnapshotCollector>(provider => new TelemetrySqlServerSnapshotCollector(
    provider.GetRequiredService<SqlServerSnapshotCollector>(),
    provider.GetRequiredService<IMonitorTelemetry>()));
builder.Services.AddSingleton<ServerHealthSnapshotCache>();
builder.Services.AddSingleton<IServerHealthSnapshotCache>(provider => new TelemetryServerHealthSnapshotCache(
    provider.GetRequiredService<ServerHealthSnapshotCache>(),
    provider.GetRequiredService<IMonitorTelemetry>()));
builder.Services.AddSingleton<IHealthRuleEvaluator, HealthRuleEvaluator>();
builder.Services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddSingleton<IAdvisorContextBuilder, AdvisorContextBuilder>();
builder.Services.AddSingleton<IAdvisorProvider, DisabledAdvisorProvider>();
builder.Services.AddSingleton<IIncidentWorkflowService, IncidentWorkflowService>();
builder.Services.AddSingleton<IAdvisorRequestService, AdvisorRequestService>();
builder.Services.AddSingleton<ISnapshotObserver, SnapshotObserver>();
builder.Services.AddSingleton<ITrendReadService, TrendReadService>();

var scheduleOptions = builder.Configuration.GetSection(SnapshotScheduleOptions.SectionName).Get<SnapshotScheduleOptions>() ?? new();
scheduleOptions.Validate();
builder.Services.AddSingleton(scheduleOptions);
builder.Services.AddSingleton<ICollectionBackoffPolicy, CollectionBackoffPolicy>();
builder.Services.AddSingleton<ISchedulerStatusStore>(provider => haStateOptions.UseSharedOperationalState ? new SharedSchedulerStatusStore(provider.GetRequiredService<ISharedStateDocumentStore>()) : new SchedulerStatusStore());
builder.Services.AddSingleton<SnapshotCollectionCycle>();
builder.Services.AddSingleton<ISnapshotCollectionCycle>(provider => new TelemetrySnapshotCollectionCycle(
    provider.GetRequiredService<SnapshotCollectionCycle>(),
    provider.GetRequiredService<IMonitorTelemetry>()));
builder.Services.AddHostedService<SnapshotSchedulerService>();
builder.Services.AddSingleton<IMonitorReadService, MonitorReadService>();
builder.Services.AddSingleton<ISnapshotRefreshService, SnapshotRefreshService>();

var backupOptions = builder.Configuration.GetSection(BackupStoreOptions.SectionName).Get<BackupStoreOptions>() ?? new();
backupOptions.Validate();
var backupRoot = OperationalStorePath.ResolveOutsideWebRoot(backupOptions.RootPath, builder.Environment.ContentRootPath, builder.Environment.WebRootPath);
builder.Services.AddSingleton(backupOptions);
var backupPolicyOptions = builder.Configuration.GetSection(BackupPolicyOptions.SectionName).Get<BackupPolicyOptions>() ?? new();
backupPolicyOptions.Validate();
builder.Services.AddSingleton(backupPolicyOptions);
builder.Services.AddSingleton<IOperationalRestoreWriter>(provider => new OperationalRestoreWriter(registrationStoreOptions, operationalStoreOptions, haStateOptions, provider.GetRequiredService<ISharedStateDocumentStore>(), provider.GetRequiredService<IServerRegistrationRepository>(), builder.Environment.ContentRootPath, builder.Environment.WebRootPath));
builder.Services.AddSingleton<IOperationalBackupService>(provider => new OperationalBackupService(provider.GetRequiredService<IServerRegistrationRepository>(), provider.GetRequiredService<IHealthIncidentRepository>(), provider.GetRequiredService<ISnapshotHistoryStore>(), provider.GetRequiredService<IAuditStore>(), provider.GetRequiredService<IOperationalRestoreWriter>(), backupOptions, backupRoot, provider.GetRequiredService<TimeProvider>()));

var deploymentReadiness = DeploymentReadinessEvaluator.Evaluate(deploymentTopologyOptions, sharedStateOptions, haStateOptions, coordinationOptions);
if (deploymentTopologyOptions.Mode == DeploymentTopology.MultiNode && !deploymentReadiness.Ready) throw new InvalidOperationException(deploymentReadiness.Message);
builder.Services.AddSingleton(deploymentReadiness);
builder.Services.AddSingleton<IApplicationReadinessService, ApplicationReadinessService>();
builder.Services.AddSingleton<IDbaOperationsSurfaceService, DbaOperationsSurfaceService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.Name = "Monitor.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(webSecurityOptions.SessionIdleMinutes);
    options.EventsType = typeof(AbsoluteSessionCookieEvents);
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(MonitorPolicies.Read, policy => policy.RequireRole(MonitorRoles.Viewer, MonitorRoles.Operator, MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Operate, policy => policy.RequireRole(MonitorRoles.Operator, MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Manage, policy => policy.RequireRole(MonitorRoles.Administrator));
    options.AddPolicy(MonitorPolicies.Advisor, policy => policy.RequireRole(MonitorRoles.Operator, MonitorRoles.Administrator));
});

var app = builder.Build();
var configuredRegistration = ConfiguredServerRegistrationLoader.Load(app.Configuration, app.Services.GetRequiredService<TimeProvider>());
if (configuredRegistration is not null) app.Services.GetRequiredService<IServerRegistrationRepository>().Upsert(configuredRegistration);
if (deploymentTopologyOptions.Mode == DeploymentTopology.MultiNode && !app.Services.GetRequiredService<ICredentialReadinessService>().Get().MultiNodeCredentialReady)
    throw new InvalidOperationException("Multi-node startup is blocked by credential/key-management readiness.");

if (webSecurityOptions.HasTrustedForwarders) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseStatusCodePagesWithReExecute("/error/status/{0}");
    app.UseHsts();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AuthenticationTelemetryMiddleware>();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Account}/{action=Login}/{id?}");
app.Run();