using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class MaintenanceSuppressionPolicyTests
{
    [Fact]
    public async Task B200_011_ScheduledCycleSkipsTargetDuringActiveMaintenance()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var registration = CreateRegistration(clock.GetUtcNow());
        var registrations = new InMemoryServerRegistrationRepository();
        registrations.Upsert(registration);
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(registration.Id, clock.GetUtcNow().AddMinutes(-5), clock.GetUtcNow().AddMinutes(5), suppression: false));
        var eligibility = new CollectionBackoffPolicy(clock, metadata);
        var status = new SchedulerStatusStore();
        var cache = new CountingCache();
        var cycle = new SnapshotCollectionCycle(registrations, cache, new NoOpObserver(), backoff: eligibility, status: status, timeProvider: clock);

        await cycle.RunOnceAsync(default);

        Assert.Equal(0, cache.RefreshCalls);
        Assert.Equal(1, status.Get().SkippedBackoff);
        Assert.Equal(0, status.Get().Succeeded);
    }

    [Fact]
    public async Task B200_012_ManualRefreshDuringMaintenanceIsAllowedAndAudited()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var id = Guid.NewGuid();
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(id, clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddMinutes(10), suppression: false));
        var audit = new RecordingAuditStore();
        var refresh = new RecordingRefreshService();
        var controller = new OperationsController(new DemoMonitorService(), new EmptyReadService(), audit: audit, snapshotRefresh: refresh, operatorMetadata: metadata, timeProvider: clock);
        AttachHttpContext(controller, "operator");

        var result = await controller.RefreshServer(id, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, refresh.Calls);
        Assert.Equal(2, audit.Events.Count(item => item.Action == "snapshot.refresh.maintenance-override"));
        Assert.NotNull(controller.TempData["SnapshotMaintenanceOverride"]);
    }

    [Fact]
    public async Task B200_013_ServerEstateProjectsMaintenanceAndPolicyReadiness()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var id = Guid.NewGuid();
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(id, clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddMinutes(10), suppression: false));
        var controller = new OperationsController(new DemoMonitorService(), new ServerPageReadService(id), operatorMetadata: metadata, timeProvider: clock);

        var result = await controller.Servers(cancellationToken: default);

        Assert.IsType<ViewResult>(result);
        var states = Assert.IsAssignableFrom<IReadOnlyDictionary<Guid, ServerOperatorPolicyState>>(controller.ViewData["ServerPolicyStates"]);
        Assert.True(states[id].MaintenanceActive);
        Assert.Equal(1, controller.ViewData["MaintenanceActiveCount"]);
    }

    [Fact]
    public void B200_014_SuppressionProjectsActionabilityWithoutMutatingIncidentEvidence()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var incident = CreateIncident(clock.GetUtcNow());
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(incident.RegistrationId, null, null, suppression: true));
        var service = new OperatorPolicyReadService(metadata, clock);
        var before = incident;

        var state = service.GetIncidents([incident])[incident.Id];

        Assert.True(state.AlertSuppressed);
        Assert.Equal(before, incident);
        Assert.Equal("Cached evidence remains immutable.", incident.Evidence);
    }

    [Fact]
    public async Task B200_015_AlertsExposeActionableAndSuppressedCounts()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var suppressedIncident = CreateIncident(clock.GetUtcNow());
        var actionableIncident = CreateIncident(clock.GetUtcNow(), Guid.NewGuid(), "blocking.active");
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(suppressedIncident.RegistrationId, null, null, suppression: true));
        metadata.UpsertServer(CreateMetadata(actionableIncident.RegistrationId, null, null, suppression: false));
        var workflow = new QueryWorkflow([suppressedIncident, actionableIncident]);
        var controller = new OperationsController(new DemoMonitorService(), new EmptyReadService(), workflow: workflow, operatorMetadata: metadata, timeProvider: clock);

        var result = await controller.Alerts(null, null, null, cancellationToken: default);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(1, controller.ViewData["SuppressedIncidentCount"]);
        Assert.Equal(1, controller.ViewData["ActionableIncidentCount"]);
        Assert.Equal(0, controller.ViewData["IncidentPolicyUnavailableCount"]);
    }

    [Fact]
    public void B200_016_SuppressionExpiresAutomaticallyFromClockProjection()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var id = Guid.NewGuid();
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.UpsertServer(CreateMetadata(id, null, null, suppression: true, suppressionEndUtc: clock.GetUtcNow().AddMinutes(5)));
        var service = new OperatorPolicyReadService(metadata, clock);

        Assert.True(service.GetServer(id).AlertSuppressed);
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        Assert.False(service.GetServer(id).AlertSuppressed);
    }

    [Fact]
    public void B200_017_WindowsAreStartInclusiveAndEndExclusive()
    {
        var start = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
        var end = start.AddHours(1);
        var window = new OperatorWindow(start, end, "Window boundary test");

        Assert.False(EnterpriseOperatorPolicy.IsWindowActive(window, start.AddTicks(-1)));
        Assert.True(EnterpriseOperatorPolicy.IsWindowActive(window, start));
        Assert.True(EnterpriseOperatorPolicy.IsWindowActive(window, end.AddTicks(-1)));
        Assert.False(EnterpriseOperatorPolicy.IsWindowActive(window, end));
    }

    [Fact]
    public void B200_018_IndependentNodesSeeConsistentSharedPolicyState()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var shared = new MemorySharedDocumentStore(clock);
        var writer = new SharedOperatorMetadataStore(shared, clock);
        var nodeAStore = new SharedOperatorMetadataStore(shared, clock);
        var nodeBStore = new SharedOperatorMetadataStore(shared, clock);
        var id = Guid.NewGuid();
        writer.UpsertServer(CreateMetadata(id, clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddMinutes(10), suppression: true));

        var nodeA = new OperatorPolicyReadService(nodeAStore, clock).GetServer(id);
        var nodeB = new OperatorPolicyReadService(nodeBStore, clock).GetServer(id);

        Assert.Equal(nodeA.RegistrationId, nodeB.RegistrationId);
        Assert.Equal(nodeA.PolicyReadable, nodeB.PolicyReadable);
        Assert.Equal(nodeA.MaintenanceActive, nodeB.MaintenanceActive);
        Assert.Equal(nodeA.AlertSuppressed, nodeB.AlertSuppressed);
        Assert.Equal(nodeA.Environment, nodeB.Environment);
        Assert.Equal(nodeA.Group, nodeB.Group);
        Assert.Equal(nodeA.Tags, nodeB.Tags);
        Assert.True(nodeA.MaintenanceActive);
        Assert.True(nodeA.AlertSuppressed);
    }

    [Fact]
    public void B200_019_CorruptPolicyMetadataFailsClosedForScheduledCollection()
    {
        var service = new OperatorPolicyReadService(new CorruptMetadataStore(), TimeProvider.System);
        var id = Guid.NewGuid();

        var state = service.GetServer(id);

        Assert.False(state.PolicyReadable);
        Assert.True(state.MaintenanceActive);
        Assert.False(service.IsScheduledCollectionAllowed(id));
    }

    [Fact]
    public void B200_020_ViewsDeclareMaintenanceAndSuppressionSemantics()
    {
        var repo = FindRepoRoot();
        var servers = File.ReadAllText(Path.Combine(repo, "src", "Monitor.Web", "Views", "Operations", "Servers.cshtml"));
        var alerts = File.ReadAllText(Path.Combine(repo, "src", "Monitor.Web", "Views", "Operations", "Alerts.cshtml"));

        Assert.Contains("SCHEDULE PAUSED", servers, StringComparison.Ordinal);
        Assert.Contains("POLICY UNAVAILABLE", servers, StringComparison.Ordinal);
        Assert.Contains("SUPPRESSED ON PAGE", alerts, StringComparison.Ordinal);
        Assert.Contains("never rewrites incident evidence", alerts, StringComparison.Ordinal);
    }

    private static ServerRegistration CreateRegistration(DateTimeOffset now) => new(
        Guid.NewGuid(),
        "SQL-01",
        new SqlServerEndpoint("sql01.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        now);

    private static ServerOperatorMetadata CreateMetadata(
        Guid id,
        DateTimeOffset? maintenanceStartUtc,
        DateTimeOffset? maintenanceEndUtc,
        bool suppression,
        DateTimeOffset? suppressionEndUtc = null)
    {
        var now = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
        var maintenance = maintenanceStartUtc is not null && maintenanceEndUtc is not null
            ? new OperatorWindow(maintenanceStartUtc.Value, maintenanceEndUtc.Value, "Approved maintenance")
            : null;
        var suppressionWindow = suppression
            ? new OperatorWindow(now.AddMinutes(-1), suppressionEndUtc ?? now.AddMinutes(10), "Approved suppression")
            : null;
        return new(id, ServerEnvironmentClass.Production, "core", ["tier-1"], maintenance, suppressionWindow, now);
    }

    private static HealthIncident CreateIncident(DateTimeOffset now, Guid? registrationId = null, string ruleId = "memory.pressure")
    {
        var id = registrationId ?? Guid.NewGuid();
        return new HealthIncident($"{id:N}:{ruleId}", id, ruleId, FindingSeverity.Warning, "Policy test incident", "Cached evidence remains immutable.", now, now, 1, IncidentStatus.Open);
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class CountingCache : IServerHealthSnapshotCache
    {
        public int RefreshCalls { get; private set; }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            throw new InvalidOperationException("Maintenance test should not reach collection.");
        }
    }

    private sealed class NoOpObserver : ISnapshotObserver
    {
        public void Observe(SnapshotCacheResult result) { }
    }

    private sealed class RecordingRefreshService : ISnapshotRefreshService
    {
        public int Calls { get; private set; }
        public Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new SnapshotRefreshResult(SnapshotRefreshStatus.Refreshed, "Snapshot refreshed.", Freshness: SnapshotFreshness.Fresh));
        }
    }

    private sealed class RecordingAuditStore : IAuditStore
    {
        public List<(string Actor, string Action, string Target, string Outcome)> Events { get; } = [];
        public void Append(string actor, string action, string target, string outcome) => Events.Add((actor, action, target, outcome));
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => [];
    }

    private sealed class ServerPageReadService(Guid id) : IMonitorReadService
    {
        private readonly ServerCard _server = new(id.ToString("D"), "SQL-01", "2022", "Enterprise", HealthState.Healthy, 20, 40, 5, 5, 1, 1, 1, ServerDataSource.LiveFresh);
        public Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServerCard>>([_server]);
        public Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new ServerEstatePage([_server], 0, 50, 1));
        public Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ServerDetailsViewModel?>(null);
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

    private sealed class QueryWorkflow(IReadOnlyList<HealthIncident> incidents) : IIncidentWorkflowService
    {
        public IncidentCenterViewModel Query(IncidentQuery query) => new(incidents, new IncidentSummary(incidents.Count, 0, 0, 0, incidents.Count), query);
        public Task<IncidentDetailsViewModel?> GetDetailsAsync(string id, CancellationToken cancellationToken) => Task.FromResult<IncidentDetailsViewModel?>(null);
        public bool Acknowledge(string id) => false;
        public bool Resolve(string id) => false;
        public bool Reopen(string id) => false;
    }

    private sealed class CorruptMetadataStore : IOperatorMetadataStore
    {
        public ServerOperatorMetadata GetServer(Guid registrationId) => throw new InvalidDataException("Corrupt operator policy state.");
        public void UpsertServer(ServerOperatorMetadata metadata) => throw new NotSupportedException();
        public IncidentOperatorMetadata GetIncident(string incidentId) => throw new InvalidDataException("Corrupt operator policy state.");
        public void AssignIncident(string incidentId, string? assignee) => throw new NotSupportedException();
        public void AddIncidentNote(string incidentId, string actor, string note) => throw new NotSupportedException();
        public void SetRecommendationAcknowledged(string incidentId, string recommendationKey, bool acknowledged) => throw new NotSupportedException();
        public EnterpriseOperatorSnapshot Snapshot() => throw new InvalidDataException("Corrupt operator policy state.");
    }

    private sealed class MemorySharedDocumentStore(TimeProvider clock) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _documents.TryGetValue(key, out var value);
                return Task.FromResult(value);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _documents.TryGetValue(key, out var current);
                var version = current?.Version ?? 0;
                if (version != expectedVersion) return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                var next = new SharedStateDocument(key, version + 1, payloadJson, clock.GetUtcNow());
                _documents[key] = next;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, next));
            }
        }
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(_values, StringComparer.Ordinal);
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = new Dictionary<string, object>(values, StringComparer.Ordinal);
    }

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
