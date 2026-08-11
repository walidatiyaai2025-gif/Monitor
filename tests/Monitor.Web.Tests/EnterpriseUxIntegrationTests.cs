using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnterpriseUxIntegrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Shell_ExposesEnterpriseOperationsAsPrimaryNavigation()
    {
        var layout = Read("src/Monitor.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("Enterprise Operations", layout, StringComparison.Ordinal);
        Assert.Contains("@Active(\"/enterprise\", exact: true)", layout, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"EnterpriseOperations\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerDetails_ProjectsEnterpriseMetadataWithoutCollection()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.UpsertServer(new ServerOperatorMetadata(
            id,
            ServerEnvironmentClass.Production,
            "payments",
            ["tier-1", "primary"],
            new OperatorWindow(now.AddMinutes(-5), now.AddMinutes(5), "Change window"),
            new OperatorWindow(now.AddMinutes(-5), now.AddMinutes(5), "Noise control"),
            now));
        var read = new DetailsReadService(id);
        var controller = new OperationsController(
            new DemoMonitorService(),
            read,
            operatorMetadata: metadata,
            timeProvider: TimeProvider.System);

        var result = await controller.ServerDetails(id.ToString("D"), default);

        Assert.IsType<ViewResult>(result);
        var projected = Assert.IsType<ServerOperatorMetadata>(controller.ViewData["ServerOperatorMetadata"]);
        Assert.Equal(ServerEnvironmentClass.Production, projected.Environment);
        Assert.Equal("payments", projected.Group);
        Assert.Equal(true, controller.ViewData["MaintenanceActive"]);
        Assert.Equal(true, controller.ViewData["AlertSuppressed"]);
        Assert.Equal(1, read.ServerReads);
    }

    [Fact]
    public async Task IncidentDetails_ProjectsOwnerNotesAndCurrentRecommendationState()
    {
        var incident = CreateIncident();
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.AssignIncident(incident.Id, "DBA-OnCall");
        metadata.AddIncidentNote(incident.Id, "operator", "Reviewed cached evidence and assigned follow-up.");
        var recommendation = new RecommendationEngine().Build(incident)!;
        var key = RecommendationAcknowledgmentKey.Create(recommendation);
        metadata.SetRecommendationAcknowledged(incident.Id, key, true);
        var controller = new OperationsController(
            new DemoMonitorService(),
            new EmptyReadService(),
            workflow: new DetailsWorkflow(incident, recommendation),
            operatorMetadata: metadata);

        var result = await controller.IncidentDetails(incident.Id, default);

        Assert.IsType<ViewResult>(result);
        var projected = Assert.IsType<IncidentOperatorMetadata>(controller.ViewData["IncidentOperatorMetadata"]);
        Assert.Equal("DBA-OnCall", projected.Assignee);
        Assert.Single(projected.Notes);
        Assert.Equal(key, controller.ViewData["RecommendationKey"]);
        Assert.Equal(true, controller.ViewData["RecommendationAcknowledged"]);
    }

    [Fact]
    public void Overview_FiltersEstateAndIncidentsUsingControlPlaneMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var registrationId = Guid.NewGuid();
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(new ServerRegistration(
            registrationId,
            "SQL-PAYMENTS-01",
            new SqlServerEndpoint("sql-payments.internal", 1433),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            now));
        var incidents = new InMemoryHealthIncidentRepository();
        incidents.Apply([new HealthFinding(registrationId, "memory.pressure", FindingSeverity.Warning, "Memory pressure", "Cached low-memory signal.", now)]);
        var incident = Assert.Single(incidents.GetAll());
        var metadata = new InMemoryOperatorMetadataStore(TimeProvider.System);
        metadata.UpsertServer(new ServerOperatorMetadata(
            registrationId,
            ServerEnvironmentClass.Production,
            "payments",
            ["tier-1"],
            null,
            new OperatorWindow(now.AddMinutes(-5), now.AddMinutes(5), "Planned alert noise control"),
            now));
        metadata.AssignIncident(incident.Id, "DBA-OnCall");
        var controller = CreateEnterpriseController(metadata, registrations, incidents, new RecordingAuditStore());

        var result = controller.Overview(ServerEnvironmentClass.Production, "payments", "tier-1", "DBA-OnCall", true);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EnterpriseOperationsViewModel>(view.Model);
        Assert.Single(model.Servers);
        var row = Assert.Single(model.Incidents);
        Assert.True(row.AlertSuppressed);
        Assert.Equal("DBA-OnCall", row.Metadata.Assignee);
    }

    [Fact]
    public void InvalidOperatorNote_UsesPrgSafeErrorAndAuditsRejection()
    {
        var incident = CreateIncident();
        var incidents = new InMemoryHealthIncidentRepository();
        incidents.Apply([new HealthFinding(incident.RegistrationId, incident.RuleId, incident.Severity, incident.Title, incident.Evidence, incident.LastSeenUtc)]);
        var audit = new RecordingAuditStore();
        var controller = CreateEnterpriseController(new InMemoryOperatorMetadataStore(TimeProvider.System), new InMemoryServerRegistrationRepository(), incidents, audit);
        AttachHttpContext(controller, "operator");

        var result = controller.AddIncidentNote(incident.Id, "Password=SuperSecret;Data Source=sql01", "test-request-0001");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["OperatorError"]);
        var rejected = Assert.Single(audit.Events, item => item.Action == "incident.note");
        Assert.Equal("rejected", rejected.Outcome);
        Assert.DoesNotContain("SuperSecret", rejected.Target, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperSecret", controller.TempData["OperatorError"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EnterpriseMutations_RequirePostAntiforgeryAndNamedAuthorizationPolicy()
    {
        var names = new[]
        {
            nameof(EnterpriseOperationsController.UpdateServerProfile),
            nameof(EnterpriseOperationsController.AssignIncident),
            nameof(EnterpriseOperationsController.AddIncidentNote),
            nameof(EnterpriseOperationsController.AcknowledgeRecommendation)
        };

        foreach (var name in names)
        {
            var method = typeof(EnterpriseOperationsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
            Assert.True(authorization.Policy is MonitorPolicies.Operate or MonitorPolicies.Manage);
        }
    }

    [Fact]
    public void EnterpriseViews_ExposeAccessibleFiltersAndCoreDetailContext()
    {
        var overview = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");
        var server = Read("src/Monitor.Web/Views/Operations/ServerDetails.cshtml");
        var incident = Read("src/Monitor.Web/Views/Operations/IncidentDetails.cshtml");

        Assert.Contains("aria-label=\"Enterprise operations filters\"", overview, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", overview, StringComparison.Ordinal);
        Assert.Contains("Environment", server, StringComparison.Ordinal);
        Assert.Contains("Alert suppression", server, StringComparison.Ordinal);
        Assert.Contains("Operator context", incident, StringComparison.Ordinal);
        Assert.Contains("Latest operator notes", incident, StringComparison.Ordinal);
    }

    private static EnterpriseOperationsController CreateEnterpriseController(
        IOperatorMetadataStore metadata,
        IServerRegistrationRepository registrations,
        IHealthIncidentRepository incidents,
        RecordingAuditStore audit) =>
        new(
            metadata,
            registrations,
            incidents,
            new RecommendationEngine(),
            new EmptyCsvReportService(),
            new EmptyDiagnosticsPackageService(),
            audit,
            TimeProvider.System);

    private static HealthIncident CreateIncident()
    {
        var now = DateTimeOffset.UtcNow;
        var registrationId = Guid.NewGuid();
        return new HealthIncident(
            $"{registrationId:N}:memory.pressure",
            registrationId,
            "memory.pressure",
            FindingSeverity.Warning,
            "Memory pressure",
            "Cached low-memory signal.",
            now,
            now,
            1,
            IncidentStatus.Open);
    }

    private static void AttachHttpContext(Controller controller, string name)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new MemoryTempDataProvider());
    }

    private sealed class RecordingAuditStore : IAuditStore
    {
        public List<(string Actor, string Action, string Target, string Outcome)> Events { get; } = [];
        public void Append(string actor, string action, string target, string outcome) => Events.Add((actor, action, target, outcome));
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => [];
    }

    private sealed class EmptyCsvReportService : ISafeCsvReportService
    {
        public byte[] BuildServerReport() => [];
    }

    private sealed class EmptyDiagnosticsPackageService : IRedactedDiagnosticsPackageService
    {
        public Task<byte[]> BuildAsync(CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class DetailsReadService(Guid id) : IMonitorReadService
    {
        public int ServerReads { get; private set; }
        public Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServerCard>>([]);
        public Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new ServerEstatePage([], 0, 50, 0));
        public Task<ServerDetailsViewModel?> GetServerAsync(string value, CancellationToken cancellationToken = default)
        {
            ServerReads++;
            return Task.FromResult<ServerDetailsViewModel?>(new ServerDetailsViewModel
            {
                Server = new ServerCard(id.ToString("D"), "SQL-01", "2022", "Enterprise", HealthState.Healthy, 0, 40, 5, 5, 0, 0, 1, ServerDataSource.LiveFresh),
                Metrics = []
            });
        }
        public Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HealthModuleServerViewModel>>([]);
        public Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IncidentRow>>([]);
        public Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EmptyReadService : IMonitorReadService
    {
        public Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServerCard>>([]);
        public Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new ServerEstatePage([], 0, 50, 0));
        public Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ServerDetailsViewModel?>(null);
        public Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HealthModuleServerViewModel>>([]);
        public Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IncidentRow>>([]);
        public Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DetailsWorkflow(HealthIncident incident, RecommendationPlan recommendation) : IIncidentWorkflowService
    {
        public IncidentCenterViewModel Query(IncidentQuery query) => new([], new IncidentSummary(0, 0, 0, 0, 0), query);
        public Task<IncidentDetailsViewModel?> GetDetailsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<IncidentDetailsViewModel?>(new(incident, recommendation, new AdvisorResult(AdvisorStatus.Disabled, "Disabled")));
        public bool Acknowledge(string id) => false;
        public bool Resolve(string id) => false;
        public bool Reopen(string id) => false;
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(_values, StringComparer.Ordinal);
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = new Dictionary<string, object>(values, StringComparer.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
