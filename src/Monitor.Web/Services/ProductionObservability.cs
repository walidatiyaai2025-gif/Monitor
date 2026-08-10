using System.Collections.Concurrent;
using System.Diagnostics;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum SecurityTelemetryOutcome
{
    Succeeded,
    Rejected,
    Limited
}

public sealed record MonitorTelemetrySnapshot(
    long CollectorAttempts,
    long CollectorSucceeded,
    long CollectorFailed,
    string? LastCollectorFailureCategory,
    DateTimeOffset? LastCollectorSuccessUtc,
    DateTimeOffset? LastCollectorFailureUtc,
    long CacheFreshReads,
    long CacheStaleReads,
    long CacheMisses,
    long CacheRefreshes,
    long CacheCoalescedWaits,
    long SchedulerCycles,
    long SchedulerSucceeded,
    long SchedulerFailed,
    long IncidentObservations,
    long IncidentTransitionsApplied,
    long IncidentTransitionsRejected,
    int ActiveIncidents,
    long LoginSucceeded,
    long LoginRejected,
    long LoginLimited,
    DateTimeOffset CapturedAtUtc);

public interface IMonitorTelemetry
{
    MonitorTelemetrySnapshot Snapshot();
    void CollectorAttempt();
    void CollectorSucceeded();
    void CollectorFailed(string category);
    void CacheFreshRead();
    void CacheStaleRead();
    void CacheMiss();
    void CacheRefresh();
    void CacheCoalescedWait();
    void SchedulerCycleSucceeded();
    void SchedulerCycleFailed();
    void IncidentObserved(int activeCount);
    void IncidentTransition(bool applied);
    void Login(SecurityTelemetryOutcome outcome);
}

public sealed class MonitorTelemetry(TimeProvider timeProvider) : IMonitorTelemetry
{
    private long _collectorAttempts;
    private long _collectorSucceeded;
    private long _collectorFailed;
    private long _cacheFreshReads;
    private long _cacheStaleReads;
    private long _cacheMisses;
    private long _cacheRefreshes;
    private long _cacheCoalescedWaits;
    private long _schedulerCycles;
    private long _schedulerSucceeded;
    private long _schedulerFailed;
    private long _incidentObservations;
    private long _incidentTransitionsApplied;
    private long _incidentTransitionsRejected;
    private long _loginSucceeded;
    private long _loginRejected;
    private long _loginLimited;
    private int _activeIncidents;
    private readonly object _collectorGate = new();
    private string? _lastCollectorFailureCategory;
    private DateTimeOffset? _lastCollectorSuccessUtc;
    private DateTimeOffset? _lastCollectorFailureUtc;

    public MonitorTelemetrySnapshot Snapshot()
    {
        string? failureCategory;
        DateTimeOffset? successUtc;
        DateTimeOffset? failureUtc;
        lock (_collectorGate)
        {
            failureCategory = _lastCollectorFailureCategory;
            successUtc = _lastCollectorSuccessUtc;
            failureUtc = _lastCollectorFailureUtc;
        }

        return new(
            Interlocked.Read(ref _collectorAttempts),
            Interlocked.Read(ref _collectorSucceeded),
            Interlocked.Read(ref _collectorFailed),
            failureCategory,
            successUtc,
            failureUtc,
            Interlocked.Read(ref _cacheFreshReads),
            Interlocked.Read(ref _cacheStaleReads),
            Interlocked.Read(ref _cacheMisses),
            Interlocked.Read(ref _cacheRefreshes),
            Interlocked.Read(ref _cacheCoalescedWaits),
            Interlocked.Read(ref _schedulerCycles),
            Interlocked.Read(ref _schedulerSucceeded),
            Interlocked.Read(ref _schedulerFailed),
            Interlocked.Read(ref _incidentObservations),
            Interlocked.Read(ref _incidentTransitionsApplied),
            Interlocked.Read(ref _incidentTransitionsRejected),
            Volatile.Read(ref _activeIncidents),
            Interlocked.Read(ref _loginSucceeded),
            Interlocked.Read(ref _loginRejected),
            Interlocked.Read(ref _loginLimited),
            timeProvider.GetUtcNow());
    }

    public void CollectorAttempt() => Interlocked.Increment(ref _collectorAttempts);

    public void CollectorSucceeded()
    {
        Interlocked.Increment(ref _collectorSucceeded);
        lock (_collectorGate)
        {
            _lastCollectorSuccessUtc = timeProvider.GetUtcNow();
        }
    }

    public void CollectorFailed(string category)
    {
        Interlocked.Increment(ref _collectorFailed);
        lock (_collectorGate)
        {
            _lastCollectorFailureCategory = BoundCategory(category);
            _lastCollectorFailureUtc = timeProvider.GetUtcNow();
        }
    }

    public void CacheFreshRead() => Interlocked.Increment(ref _cacheFreshReads);
    public void CacheStaleRead() => Interlocked.Increment(ref _cacheStaleReads);
    public void CacheMiss() => Interlocked.Increment(ref _cacheMisses);
    public void CacheRefresh() => Interlocked.Increment(ref _cacheRefreshes);
    public void CacheCoalescedWait() => Interlocked.Increment(ref _cacheCoalescedWaits);

    public void SchedulerCycleSucceeded()
    {
        Interlocked.Increment(ref _schedulerCycles);
        Interlocked.Increment(ref _schedulerSucceeded);
    }

    public void SchedulerCycleFailed()
    {
        Interlocked.Increment(ref _schedulerCycles);
        Interlocked.Increment(ref _schedulerFailed);
    }

    public void IncidentObserved(int activeCount)
    {
        Interlocked.Increment(ref _incidentObservations);
        Volatile.Write(ref _activeIncidents, Math.Max(0, activeCount));
    }

    public void IncidentTransition(bool applied)
    {
        if (applied) Interlocked.Increment(ref _incidentTransitionsApplied);
        else Interlocked.Increment(ref _incidentTransitionsRejected);
    }

    public void Login(SecurityTelemetryOutcome outcome)
    {
        switch (outcome)
        {
            case SecurityTelemetryOutcome.Succeeded: Interlocked.Increment(ref _loginSucceeded); break;
            case SecurityTelemetryOutcome.Rejected: Interlocked.Increment(ref _loginRejected); break;
            case SecurityTelemetryOutcome.Limited: Interlocked.Increment(ref _loginLimited); break;
            default: throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private static string BoundCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        var safe = new string(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-').Take(48).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }
}

public sealed class TelemetrySqlServerSnapshotCollector(
    ISqlServerSnapshotCollector inner,
    IMonitorTelemetry telemetry) : ISqlServerSnapshotCollector
{
    public async Task<ServerHealthSnapshot> CollectAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
    {
        telemetry.CollectorAttempt();
        try
        {
            var result = await inner.CollectAsync(registration, cancellationToken);
            telemetry.CollectorSucceeded();
            return result;
        }
        catch (SnapshotCollectionException exception)
        {
            telemetry.CollectorFailed(exception.Failure.ToString());
            throw;
        }
        catch
        {
            telemetry.CollectorFailed("Unexpected");
            throw;
        }
    }
}

public sealed class TelemetryServerHealthSnapshotCache(
    IServerHealthSnapshotCache inner,
    IMonitorTelemetry telemetry) : IServerHealthSnapshotCache
{
    private readonly ConcurrentDictionary<Guid, int> _activeReads = new();
    private readonly ConcurrentDictionary<Guid, int> _activeRefreshes = new();

    public async Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
    {
        var active = _activeReads.AddOrUpdate(registration.Id, 1, static (_, current) => current + 1);
        if (active > 1) telemetry.CacheCoalescedWait();
        var before = telemetry.Snapshot().CollectorAttempts;
        try
        {
            var result = await inner.GetAsync(registration, cancellationToken);
            var after = telemetry.Snapshot().CollectorAttempts;
            if (after > before) telemetry.CacheMiss();
            if (result.Freshness == SnapshotFreshness.Stale) telemetry.CacheStaleRead();
            else telemetry.CacheFreshRead();
            return result;
        }
        finally
        {
            Release(_activeReads, registration.Id);
        }
    }

    public async Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
    {
        telemetry.CacheRefresh();
        var active = _activeRefreshes.AddOrUpdate(registration.Id, 1, static (_, current) => current + 1);
        if (active > 1) telemetry.CacheCoalescedWait();
        try
        {
            var result = await inner.RefreshAsync(registration, cancellationToken);
            if (result.Freshness == SnapshotFreshness.Stale) telemetry.CacheStaleRead();
            else telemetry.CacheFreshRead();
            return result;
        }
        finally
        {
            Release(_activeRefreshes, registration.Id);
        }
    }

    private static void Release(ConcurrentDictionary<Guid, int> values, Guid id)
    {
        while (values.TryGetValue(id, out var current))
        {
            if (current <= 1)
            {
                values.TryRemove(id, out _);
                return;
            }
            if (values.TryUpdate(id, current - 1, current)) return;
        }
    }
}

public sealed class TelemetrySnapshotCollectionCycle(
    ISnapshotCollectionCycle inner,
    IMonitorTelemetry telemetry) : ISnapshotCollectionCycle
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.RunOnceAsync(cancellationToken);
            telemetry.SchedulerCycleSucceeded();
        }
        catch
        {
            telemetry.SchedulerCycleFailed();
            throw;
        }
    }
}

public sealed class TelemetryHealthIncidentRepository(
    IHealthIncidentRepository inner,
    IMonitorTelemetry telemetry) : IHealthIncidentRepository
{
    public void Apply(IEnumerable<HealthFinding> findings)
    {
        inner.Apply(findings);
        Observe();
    }

    public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve)
    {
        inner.Reconcile(registrationId, observedAtUtc, activeFindings, canResolve);
        Observe();
    }

    public IReadOnlyList<HealthIncident> GetAll() => inner.GetAll();
    public HealthIncident? GetById(string id) => inner.GetById(id);

    public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next)
    {
        var applied = inner.TrySetStatus(id, expected, next);
        telemetry.IncidentTransition(applied);
        Observe();
        return applied;
    }

    private void Observe()
    {
        var active = inner.GetAll().Count(item => item.Status != IncidentStatus.Resolved);
        telemetry.IncidentObserved(active);
    }
}

public enum ApplicationReadinessStatus
{
    Ready,
    Degraded,
    NotReady
}

public sealed record ApplicationReadinessSnapshot(
    ApplicationReadinessStatus Status,
    string Message,
    SharedStateReadinessStatus SharedStateStatus,
    bool DeploymentReady,
    bool CredentialReady,
    bool BackupReady,
    DateTimeOffset CheckedAtUtc);

public interface IApplicationReadinessService
{
    Task<ApplicationReadinessSnapshot> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class ApplicationReadinessService(
    DeploymentReadinessViewModel deployment,
    ISharedStateReadinessService sharedStateReadiness,
    ICredentialReadinessService credentialReadiness,
    IOperationalBackupService backupService,
    SharedStateOptions sharedStateOptions,
    TimeProvider timeProvider) : IApplicationReadinessService
{
    public async Task<ApplicationReadinessSnapshot> CheckAsync(CancellationToken cancellationToken = default)
    {
        var shared = sharedStateOptions.Provider == SharedStateProviderKind.Disabled
            ? SharedStateReadinessViewModel.Disabled()
            : await sharedStateReadiness.GetAsync(cancellationToken);
        var credentials = credentialReadiness.Get();
        var backup = backupService.GetReadiness();
        var sharedRequired = sharedStateOptions.Provider != SharedStateProviderKind.Disabled;
        var sharedReady = !sharedRequired || shared.SharedStorageReady;
        var status = deployment.Ready && sharedReady
            ? ApplicationReadinessStatus.Ready
            : ApplicationReadinessStatus.NotReady;
        var message = status == ApplicationReadinessStatus.Ready
            ? "Application control-plane readiness checks passed."
            : "One or more control-plane readiness checks are not ready.";
        return new(status, message, shared.Status, deployment.Ready, credentials.MultiNodeCredentialReady, backup.Ready, timeProvider.GetUtcNow());
    }
}

public sealed record ObservabilityViewModel(
    ApplicationReadinessSnapshot Readiness,
    MonitorTelemetrySnapshot Telemetry);

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsSafe(incoming) ? incoming! : Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            finally
            {
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                logger.LogInformation(
                    "HTTP {Method} completed {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Response.StatusCode,
                    Math.Round(elapsedMs, 1));
            }
        }
    }

    internal static bool IsSafe(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
