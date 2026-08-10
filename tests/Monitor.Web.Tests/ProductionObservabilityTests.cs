using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ProductionObservabilityTests
{
    [Fact]
    public void TelemetrySnapshot_IsAggregateOnlyAndBoundsFailureCategory()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var telemetry = new MonitorTelemetry(time);
        telemetry.CollectorAttempt();
        telemetry.CollectorFailed("Sql.Timeout provider-secret=canary-password !@#$%^&*() very-long-value-that-must-be-truncated");
        telemetry.CacheFreshRead();
        telemetry.CacheStaleRead();
        telemetry.CacheMiss();
        telemetry.CacheRefresh();
        telemetry.CacheCoalescedWait();
        telemetry.SchedulerCycleSucceeded();
        telemetry.IncidentObserved(3);
        telemetry.IncidentTransition(true);
        telemetry.Login(SecurityTelemetryOutcome.Rejected);

        var snapshot = telemetry.Snapshot();
        var text = snapshot.ToString();

        Assert.Equal(1, snapshot.CollectorAttempts);
        Assert.Equal(1, snapshot.CollectorFailed);
        Assert.True(snapshot.LastCollectorFailureCategory!.Length <= 48);
        Assert.DoesNotContain("=", snapshot.LastCollectorFailureCategory, StringComparison.Ordinal);
        Assert.DoesNotContain("canary-password", text, StringComparison.Ordinal);
        Assert.Equal(3, snapshot.ActiveIncidents);
        Assert.Equal(1, snapshot.LoginRejected);
    }

    [Fact]
    public async Task CollectorDecorator_RecordsSuccessWithoutChangingSnapshot()
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var registration = Registration();
        var expected = Snapshot(registration.Id);
        var decorator = new TelemetrySqlServerSnapshotCollector(new FakeCollector(expected), telemetry);

        var actual = await decorator.CollectAsync(registration);

        Assert.Same(expected, actual);
        Assert.Equal(1, telemetry.Snapshot().CollectorAttempts);
        Assert.Equal(1, telemetry.Snapshot().CollectorSucceeded);
        Assert.Equal(0, telemetry.Snapshot().CollectorFailed);
    }

    [Fact]
    public async Task CollectorDecorator_RedactsUnexpectedFailureToCategory()
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var decorator = new TelemetrySqlServerSnapshotCollector(new ThrowingCollector("connection string canary"), telemetry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.CollectAsync(Registration()));

        var snapshot = telemetry.Snapshot();
        Assert.Equal("Unexpected", snapshot.LastCollectorFailureCategory);
        Assert.DoesNotContain("canary", snapshot.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CacheDecorator_RecordsRefreshAndFreshness()
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var registration = Registration();
        var cache = new TelemetryServerHealthSnapshotCache(new FakeCache(SnapshotResult(registration.Id)), telemetry);

        await cache.RefreshAsync(registration);
        await cache.GetAsync(registration);

        var snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.CacheRefreshes);
        Assert.Equal(2, snapshot.CacheFreshReads);
    }

    [Fact]
    public async Task SchedulerDecorator_RecordsCycleSuccessAndFailure()
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        await new TelemetrySnapshotCollectionCycle(new FakeCycle(false), telemetry).RunOnceAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TelemetrySnapshotCollectionCycle(new FakeCycle(true), telemetry).RunOnceAsync(default));

        var snapshot = telemetry.Snapshot();
        Assert.Equal(2, snapshot.SchedulerCycles);
        Assert.Equal(1, snapshot.SchedulerSucceeded);
        Assert.Equal(1, snapshot.SchedulerFailed);
    }

    [Fact]
    public void IncidentDecorator_RecordsObservationAndTransitionWithoutEvidenceCopy()
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var inner = new InMemoryHealthIncidentRepository();
        var repository = new TelemetryHealthIncidentRepository(inner, telemetry);
        var registrationId = Guid.NewGuid();
        repository.Apply([
            new HealthFinding(registrationId, "database.unavailable", FindingSeverity.Critical, "Database unavailable", "canary-evidence-never-in-telemetry", DateTimeOffset.UtcNow)
        ]);
        var incident = repository.GetAll().Single();
        Assert.True(repository.TrySetStatus(incident.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));

        var snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.ActiveIncidents);
        Assert.Equal(1, snapshot.IncidentTransitionsApplied);
        Assert.DoesNotContain("canary-evidence", snapshot.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LivenessEndpoint_HasNoReadinessOrCollectorDependency()
    {
        var readiness = new FakeReadiness(ApplicationReadinessStatus.NotReady);
        var controller = new HealthController(readiness, new MonitorTelemetry(TimeProvider.System));

        var result = controller.Live();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, readiness.Calls);
    }

    [Fact]
    public async Task ReadyAndHealth_UseControlPlaneReadinessOnly()
    {
        var readiness = new FakeReadiness(ApplicationReadinessStatus.Ready);
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var controller = new HealthController(readiness, telemetry);

        var ready = await controller.Ready(default);
        var health = await controller.Health(default);

        Assert.IsType<OkObjectResult>(ready);
        Assert.IsType<OkObjectResult>(health);
        Assert.Equal(2, readiness.Calls);
        Assert.Equal(0, telemetry.Snapshot().CollectorAttempts);
    }

    [Fact]
    public async Task ApplicationReadiness_WhenSharedStateDisabled_DoesNotProbeSharedProvider()
    {
        var shared = new FakeSharedReadiness();
        var service = new ApplicationReadinessService(
            DeploymentReadinessViewModel.SafeDefault(),
            shared,
            new FakeCredentialReadiness(),
            new FakeBackupService(),
            new SharedStateOptions { Provider = SharedStateProviderKind.Disabled },
            TimeProvider.System);

        var result = await service.CheckAsync();

        Assert.Equal(ApplicationReadinessStatus.Ready, result.Status);
        Assert.Equal(0, shared.Calls);
    }

    [Fact]
    public async Task ApplicationReadiness_SharedProviderUnavailable_ReturnsNotReadyWithSafeMessage()
    {
        var shared = new FakeSharedReadiness(SharedStateReadinessViewModel.Unavailable("Shared-state provider is unavailable."));
        var service = new ApplicationReadinessService(
            DeploymentReadinessViewModel.SafeDefault(),
            shared,
            new FakeCredentialReadiness(),
            new FakeBackupService(),
            new SharedStateOptions { Provider = SharedStateProviderKind.SqlServer },
            TimeProvider.System);

        var result = await service.CheckAsync();

        Assert.Equal(ApplicationReadinessStatus.NotReady, result.Status);
        Assert.Equal(1, shared.Calls);
        Assert.DoesNotContain("connection", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("abc-123_OK", true)]
    [InlineData("with space", false)]
    [InlineData("../../secret", false)]
    [InlineData("", false)]
    public void CorrelationIdValidation_IsStrictAndBounded(string value, bool expected) =>
        Assert.Equal(expected, CorrelationIdMiddleware.IsSafe(value));

    [Fact]
    public async Task CorrelationMiddleware_RejectsUnsafeIncomingValueAndNeverEchoesIt()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "../../canary-secret";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var emitted = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.Equal(32, emitted.Length);
        Assert.DoesNotContain("canary", emitted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(emitted, context.TraceIdentifier);
    }

    [Theory]
    [InlineData(302, 1, 0, 0)]
    [InlineData(200, 0, 1, 0)]
    [InlineData(429, 0, 0, 1)]
    public async Task LoginTelemetry_UsesOutcomeOnly(int statusCode, long success, long rejected, long limited)
    {
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/login";
        var middleware = new AuthenticationTelemetryMiddleware(
            nextContext =>
            {
                nextContext.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            }, telemetry);

        await middleware.InvokeAsync(context);

        var snapshot = telemetry.Snapshot();
        Assert.Equal(success, snapshot.LoginSucceeded);
        Assert.Equal(rejected, snapshot.LoginRejected);
        Assert.Equal(limited, snapshot.LoginLimited);
    }

    private static ServerRegistration Registration() => new(
        Guid.NewGuid(),
        "Observability",
        new SqlServerEndpoint("sql.example.internal", 1433),
        SqlAuthenticationMode.IntegratedSecurity,
        null,
        true,
        DateTimeOffset.UtcNow);

    private static ServerHealthSnapshot Snapshot(Guid registrationId) => new(
        registrationId,
        "SQL",
        "16.0",
        "Enterprise",
        null,
        3600,
        4,
        4,
        DateTimeOffset.UtcNow);

    private static SnapshotCacheResult SnapshotResult(Guid registrationId) => new(
        Snapshot(registrationId),
        SnapshotFreshness.Fresh,
        TimeSpan.Zero);

    private sealed class FakeCollector(ServerHealthSnapshot snapshot) : ISqlServerSnapshotCollector
    {
        public Task<ServerHealthSnapshot> CollectAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class ThrowingCollector(string message) : ISqlServerSnapshotCollector
    {
        public Task<ServerHealthSnapshot> CollectAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromException<ServerHealthSnapshot>(new InvalidOperationException(message));
    }

    private sealed class FakeCache(SnapshotCacheResult result) : IServerHealthSnapshotCache
    {
        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeCycle(bool fail) : ISnapshotCollectionCycle
    {
        public Task RunOnceAsync(CancellationToken cancellationToken)
            => fail ? Task.FromException(new InvalidOperationException("cycle failed")) : Task.CompletedTask;
    }

    private sealed class FakeReadiness(ApplicationReadinessStatus status) : IApplicationReadinessService
    {
        public int Calls { get; private set; }
        public Task<ApplicationReadinessSnapshot> CheckAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ApplicationReadinessSnapshot(
                status,
                status == ApplicationReadinessStatus.Ready ? "Ready." : "Not ready.",
                SharedStateReadinessStatus.Disabled,
                status == ApplicationReadinessStatus.Ready,
                false,
                true,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeSharedReadiness(SharedStateReadinessViewModel? result = null) : ISharedStateReadinessService
    {
        public int Calls { get; private set; }
        public Task<SharedStateReadinessViewModel> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result ?? SharedStateReadinessViewModel.Ready(1));
        }
    }

    private sealed class FakeCredentialReadiness : ICredentialReadinessService
    {
        public CredentialReadinessViewModel Get() => new(
            DataProtectionKeyStoreMode.LocalFile,
            false,
            0,
            0,
            0,
            false,
            "Single-node credential mode",
            "Credential mode is valid for the selected deployment.");
    }

    private sealed class FakeBackupService : IOperationalBackupService
    {
        public Task<BackupListItem> CreateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupValidationResult> ValidateAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupRestoreResult> RestoreAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public BackupReadinessViewModel GetReadiness() => new(true, "Backup ready", "Ready.", 0, null, false, []);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
