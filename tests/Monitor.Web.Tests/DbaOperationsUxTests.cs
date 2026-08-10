using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DbaOperationsUxTests
{
    [Fact]
    public async Task DbaSurface_UsesOneCentralReadinessSnapshotAndOpaqueNodeLabel()
    {
        var readiness = new CountingReadinessService(new ApplicationReadinessSnapshot(
            ApplicationReadinessStatus.Ready,
            "Ready.",
            SharedStateReadinessStatus.Ready,
            true,
            true,
            true,
            DateTimeOffset.UtcNow,
            SharedStateSchemaVersion: 1,
            SharedStorageReady: true));
        var backup = new FakeBackupService(new BackupReadinessViewModel(
            true,
            "Ready",
            "Backup ready.",
            2,
            DateTimeOffset.UtcNow,
            false,
            [new BackupListItem("backup-opaque-001", DateTimeOffset.UtcNow, 128)]));
        var scheduler = new SchedulerStatusStore();
        scheduler.Set(new SchedulerStatus(true, false, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, 4, 4, 0, 0));
        var service = new DbaOperationsSurfaceService(
            readiness,
            backup,
            scheduler,
            new SnapshotScheduleOptions { Enabled = true, MaxJitterSeconds = 5 },
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode });

        var result = await service.GetAsync();

        Assert.Equal(1, readiness.Calls);
        Assert.Equal("NODE-", result.NodeLabel[..5]);
        Assert.NotEqual(Environment.MachineName, result.NodeLabel);
        Assert.Equal(SharedStateReadinessStatus.Ready, result.SharedStateStatus);
        Assert.Equal(1, result.SharedStateSchemaVersion);
        Assert.Equal("backup-opaque-001", result.LatestBackupId);
        Assert.Equal("Passive / idle", result.SchedulerRole);
    }

    [Fact]
    public async Task RegisteredServerWithoutSnapshot_ReturnsRecoveryDetailsWithoutCollection()
    {
        var registrations = new InMemoryServerRegistrationRepository();
        var registration = new ServerRegistration(
            Guid.NewGuid(),
            "SQL-RECOVERY",
            new SqlServerEndpoint("sql.example.internal", 1433),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            DateTimeOffset.UtcNow);
        registrations.Upsert(registration);
        var cache = new PeekOnlyCache();
        var service = new MonitorReadService(new DemoMonitorService(), registrations, cache);

        var result = await service.GetServerAsync(registration.Id.ToString("D"));

        Assert.NotNull(result);
        Assert.Equal(ServerDataSource.RegisteredUnavailable, result!.Server.Source);
        Assert.Equal("Registered target", result.Server.Edition);
        Assert.Contains(result.Metrics, item => item.Name == "Connection" && item.Value == "Registered");
        Assert.Equal(1, cache.PeekCalls);
        Assert.Equal(0, cache.CollectionCalls);
    }

    [Fact]
    public async Task Dashboard_RequestsCentralDbaSurfaceOnce()
    {
        var read = new FakeReadService();
        var surface = new CountingDbaSurfaceService();
        var controller = new OperationsController(
            new DemoMonitorService(),
            read,
            dbaSurface: surface);

        var result = await controller.Dashboard(default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(read.Dashboard, view.Model);
        Assert.Equal(1, read.DashboardCalls);
        Assert.Equal(1, surface.Calls);
        Assert.Same(surface.Value, controller.ViewData["DbaOperationsSurface"]);
    }

    [Fact]
    public async Task IncidentFilters_RejectOverlongRuleAndPageBounded()
    {
        var workflow = new RecordingIncidentWorkflow();
        var controller = new OperationsController(
            new DemoMonitorService(),
            new FakeReadService(),
            workflow: workflow,
            performance: new PerformanceScaleOptions());
        var longRule = new string('r', 120);

        await controller.Alerts(
            IncidentStatus.Open,
            FindingSeverity.Warning,
            longRule,
            offset: -50,
            limit: 999,
            cancellationToken: default);

        Assert.NotNull(workflow.LastQuery);
        Assert.Equal(0, workflow.LastQuery!.Offset);
        Assert.Equal(100, workflow.LastQuery.Limit);
        Assert.Null(workflow.LastQuery.RuleId);
    }

    [Fact]
    public async Task RefreshServer_PersistsOutcomeClassificationForPrgFeedback()
    {
        var id = Guid.NewGuid();
        var refresh = new FakeRefreshService(new SnapshotRefreshResult(
            SnapshotRefreshStatus.Throttled,
            "Manual refresh capacity is busy.",
            RetryAfterSeconds: 2));
        var controller = new OperationsController(
            new DemoMonitorService(),
            new FakeReadService(),
            snapshotRefresh: refresh)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new MemoryTempDataProvider());

        var result = await controller.RefreshServer(id, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Manual refresh capacity is busy.", controller.TempData["SnapshotRefresh"]);
        Assert.Equal("Throttled", controller.TempData["SnapshotRefreshStatus"]);
        Assert.Equal(string.Empty, controller.TempData["SnapshotRefreshFreshness"]);
    }

    private sealed class CountingReadinessService(ApplicationReadinessSnapshot value) : IApplicationReadinessService
    {
        public int Calls { get; private set; }
        public Task<ApplicationReadinessSnapshot> CheckAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(value);
        }
    }

    private sealed class FakeBackupService(BackupReadinessViewModel readiness) : IOperationalBackupService
    {
        public Task<BackupListItem> CreateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupValidationResult> ValidateAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupRestoreResult> RestoreAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public BackupReadinessViewModel GetReadiness() => readiness;
    }

    private sealed class PeekOnlyCache : IServerHealthSnapshotCache
    {
        public int PeekCalls { get; private set; }
        public int CollectionCalls { get; private set; }
        public SnapshotCacheResult? Peek(Guid registrationId)
        {
            PeekCalls++;
            return null;
        }
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Recovery details must not collect SQL.");
        }
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CollectionCalls++;
            throw new InvalidOperationException("Recovery details must not collect SQL.");
        }
    }

    private sealed class FakeReadService : IMonitorReadService
    {
        public DashboardViewModel Dashboard { get; } = new()
        {
            Servers = [],
            Metrics = [],
            Activity = [],
            Incidents = []
        };
        public int DashboardCalls { get; private set; }
        public Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServerCard>>([]);
        public Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new ServerEstatePage([], 0, 50, 0));
        public Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ServerDetailsViewModel?>(null);
        public Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HealthModuleServerViewModel>>([]);
        public Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IncidentRow>>([]);
        public Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            DashboardCalls++;
            return Task.FromResult(Dashboard);
        }
    }

    private sealed class CountingDbaSurfaceService : IDbaOperationsSurfaceService
    {
        public int Calls { get; private set; }
        public DbaOperationsSurfaceViewModel Value { get; } = new(
            ApplicationReadinessStatus.Ready,
            "Ready.",
            DeploymentTopology.SingleNode,
            "NODE-00112233",
            SharedStateReadinessStatus.Disabled,
            null,
            false,
            "Ready",
            0,
            null,
            null,
            false,
            false,
            "Disabled",
            0,
            0,
            0,
            0,
            DateTimeOffset.UtcNow);

        public Task<DbaOperationsSurfaceViewModel> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Value);
        }
    }

    private sealed class RecordingIncidentWorkflow : IIncidentWorkflowService
    {
        public IncidentQuery? LastQuery { get; private set; }
        public IncidentCenterViewModel Query(IncidentQuery query)
        {
            LastQuery = query;
            return new IncidentCenterViewModel([], new IncidentSummary(0, 0, 0, 0, 0), query);
        }
        public Task<IncidentDetailsViewModel?> GetDetailsAsync(string id, CancellationToken cancellationToken) => Task.FromResult<IncidentDetailsViewModel?>(null);
        public bool Acknowledge(string id) => false;
        public bool Resolve(string id) => false;
        public bool Reopen(string id) => false;
    }

    private sealed class FakeRefreshService(SnapshotRefreshResult result) : ISnapshotRefreshService
    {
        public Task<SnapshotRefreshResult> RefreshAsync(Guid registrationId, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(_values, StringComparer.Ordinal);
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = new Dictionary<string, object>(values, StringComparer.Ordinal);
    }
}
