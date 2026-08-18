using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IMonitorReadService
{
    Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default);
    Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default);
    Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public sealed class MonitorReadService(
    IDemoMonitorService demo,
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IHealthIncidentRepository? incidents = null,
    PerformanceScaleOptions? performance = null) : IMonitorReadService
{
    private PerformanceScaleOptions Performance
    {
        get
        {
            var value = performance ?? new PerformanceScaleOptions();
            value.Validate();
            return value;
        }
    }

    public async Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var enabled = registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id).ToArray();
        if (enabled.Length == 0) return demo.GetServers();

        var cards = new List<ServerCard>(enabled.Length);
        foreach (var registration in enabled)
        {
            cards.Add(await TryGetLiveCardAsync(registration, cancellationToken) ?? ToUnavailableCard(registration));
        }
        return cards;
    }

    public async Task<ServerEstatePage> GetServersPageAsync(int offset, int limit, CancellationToken cancellationToken = default)
    {
        var policy = Performance;
        var boundedOffset = PerformanceScaleOptions.BoundOffset(offset);
        var boundedLimit = policy.BoundServerLimit(limit);
        var enabled = registrations.GetAll()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToArray();

        if (enabled.Length == 0)
        {
            var demoServers = demo.GetServers();
            return new(demoServers.Skip(boundedOffset).Take(boundedLimit).ToArray(), boundedOffset, boundedLimit, demoServers.Count);
        }

        var pageTargets = enabled.Skip(boundedOffset).Take(boundedLimit).ToArray();
        var cards = new List<ServerCard>(pageTargets.Length);
        foreach (var registration in pageTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            cards.Add(await TryGetLiveCardAsync(registration, cancellationToken) ?? ToUnavailableCard(registration));
        }

        return new(cards, boundedOffset, boundedLimit, enabled.Length);
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var servers = await GetServersAsync(cancellationToken);
        if (servers.All(item => item.Source == ServerDataSource.Demo)) return demo.GetDashboard();
        var incidentRows = await GetIncidentsAsync(cancellationToken);
        var online = servers.Sum(item => item.DatabaseOnline);
        var total = servers.Sum(item => item.DatabaseTotal);
        var unavailable = servers.Count(item => item.Source == ServerDataSource.RegisteredUnavailable);
        var databaseState = total == 0
            ? HealthState.Unknown
            : online < total
                ? HealthState.Warning
                : unavailable > 0
                    ? HealthState.Unknown
                    : HealthState.Healthy;
        return new DashboardViewModel
        {
            Servers = servers,
            Incidents = incidentRows,
            Metrics =
            [
                new("Registered servers", servers.Count.ToString(), "Real SQL registrations", HealthState.Unknown),
                new("Databases online", $"{online} / {total}", "From cached SQL snapshots; incomplete target coverage remains unknown", databaseState),
                new("Unavailable", unavailable.ToString(), "Registered without a usable snapshot", unavailable > 0 ? HealthState.Warning : HealthState.Healthy)
            ],
            Activity = [new("Now", "Estate projection loaded from the shared snapshot cache; navigation performed no monitored-SQL collection.", HealthState.Unknown)]
        };
    }

    public Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var registrationId)) return Task.FromResult(demo.GetServer(id));

        var registration = registrations.GetById(registrationId);
        if (registration is null || !registration.IsEnabled) return Task.FromResult<ServerDetailsViewModel?>(null);

        var result = TryPeek(registration, cancellationToken);
        var card = result is null ? ToUnavailableCard(registration) : ToLiveCard(registration, result);
        if (result is null)
        {
            return Task.FromResult<ServerDetailsViewModel?>(new ServerDetailsViewModel
            {
                Server = card,
                Metrics =
                [
                    new("Snapshot", "Unavailable", "No usable cached snapshot is available yet", HealthState.Warning),
                    new("Connection", "Registered", "Retest safely from Connection Lab", HealthState.Unknown),
                    new("Credentials", "Protected", "Current secret reference is never rendered", HealthState.Unknown),
                    new("Collection", "Pending", "Refresh after connectivity and permissions are validated", HealthState.Unknown)
                ]
            });
        }

        var snapshot = result.Snapshot;
        var memory = snapshot.Memory?.SqlProcessMemoryUtilizationPercent;
        var jobs = snapshot.Jobs;
        var jobState = jobs is null
            ? HealthState.Unknown
            : jobs.FailedLastRun > 0
                ? HealthState.Warning
                : HealthState.Healthy;
        var tempDb = AdvancedEvidenceProjection.BuildTempDb(snapshot.TempDb);
        var logRows = AdvancedEvidenceProjection.BuildTransactionLogs(snapshot.TransactionLogs);
        var logReuseBlocked = logRows.Count(item => item.TruncationBlocked == true);
        var highVlf = logRows.Count(item => item.VlfBand is LogVlfBand.High or LogVlfBand.Extreme);
        var ha = AdvancedEvidenceProjection.BuildHa(snapshot.Ha);

        return Task.FromResult<ServerDetailsViewModel?>(new ServerDetailsViewModel
        {
            Server = card,
            Metrics =
            [
                new("CPU", "Not collected", "Outside the current bounded snapshot contract", HealthState.Unknown),
                memory.HasValue
                    ? new("Memory", $"{memory.Value}%", "SQL process memory utilization", memory.Value >= 85 ? HealthState.Warning : HealthState.Healthy)
                    : new("Memory", "Not collected", "Memory evidence is unavailable in this snapshot", HealthState.Unknown),
                new("Databases", $"{card.DatabaseOnline} / {card.DatabaseTotal}", "Online databases", card.State),
                jobs is null
                    ? new("SQL Agent", "Not collected", "SQL Agent evidence is unavailable in this snapshot", HealthState.Unknown)
                    : new("SQL Agent", $"{jobs.EnabledJobs} enabled / {jobs.TotalJobs}", $"{jobs.FailedLastRun} failed on last run", jobState),
                tempDb is null
                    ? new("TempDB", "Not collected", "No bounded TempDB evidence is cached", HealthState.Unknown)
                    : new("TempDB", $"{tempDb.FileCount} / {tempDb.TotalDataFiles} files", TempDbDetail(tempDb), HealthState.Unknown),
                snapshot.TransactionLogs is null
                    ? new("Transaction log", "Not collected", "No bounded transaction-log evidence is cached", HealthState.Unknown)
                    : new("Transaction log", $"{logRows.Count} / {snapshot.TransactionLogs.TotalDatabases} DBs", $"Reuse blocked: {logReuseBlocked} · high/extreme VLF: {highVlf} · composite score not evaluated", logReuseBlocked > 0 || highVlf > 0 ? HealthState.Warning : HealthState.Unknown),
                ha is null
                    ? new("HA", "Not collected", "No bounded HA evidence is cached", HealthState.Unknown)
                    : !ha.IsHadrEnabled
                        ? new("HA", "Not enabled", "SQL Server reports HADR disabled; quorum/RPO/RTO not evaluated", HealthState.Unknown)
                        : new("HA", $"{ha.TotalReplicas} replicas", HaDetail(ha), ha.DisconnectedReplicas > 0 || ha.UnhealthyReplicas > 0 || ha.UnsynchronizedDatabases > 0 || ha.SuspendedDatabases > 0 ? HealthState.Warning : HealthState.Unknown)
            ],
            Evidence = new ServerSnapshotEvidence(
                snapshot.InstanceName,
                snapshot.UptimeSeconds,
                snapshot.CollectedAtUtc,
                snapshot.Memory,
                snapshot.Databases,
                snapshot.Backups,
                snapshot.Jobs,
                snapshot.Storage,
                snapshot.Blocking,
                snapshot.Performance,
                snapshot.TempDb,
                snapshot.TransactionLogs,
                snapshot.Ha)
        });
    }

    public Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<HealthModuleServerViewModel>();
        foreach (var registration in registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = cache.Peek(registration.Id);
                if (result is null) continue;
                var snapshot = result.Snapshot;
                rows.Add(new(
                    registration.Id.ToString("D"), snapshot.ServerName,
                    result.Freshness == SnapshotFreshness.Fresh ? ServerDataSource.LiveFresh : ServerDataSource.LiveStale,
                    (int)Math.Clamp(result.Age.TotalSeconds, 0, int.MaxValue),
                    snapshot.DatabaseOnline, snapshot.DatabaseTotal, snapshot.Databases, snapshot.Backups,
                    snapshot.Jobs, snapshot.Storage, snapshot.Blocking, snapshot.Performance, snapshot.Memory, snapshot.UptimeSeconds,
                    snapshot.TempDb, snapshot.TransactionLogs, snapshot.Ha));
            }
            catch (SnapshotCollectionException)
            {
                // A failed target is omitted; demo data is never relabeled as live.
            }
        }

        return Task.FromResult<IReadOnlyList<HealthModuleServerViewModel>>(rows);
    }

    public Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = Performance;
        IReadOnlyList<IncidentRow> rows = (incidents ??= new InMemoryHealthIncidentRepository()).GetAll()
            .Take(policy.IncidentMaxPageSize)
            .Select(item => new IncidentRow(
                item.Id,
                item.Severity.ToString(),
                item.RegistrationId.ToString("D"),
                item.Title,
                $"{Math.Max(0, (DateTimeOffset.UtcNow - item.LastSeenUtc).TotalMinutes):0}m",
                item.Status.ToString()))
            .ToArray();
        return Task.FromResult(rows);
    }

    private Task<ServerCard?> TryGetLiveCardAsync(ServerRegistration registration, CancellationToken cancellationToken)
    {
        var result = TryPeek(registration, cancellationToken);
        return Task.FromResult(result is null ? null : ToLiveCard(registration, result));
    }

    private SnapshotCacheResult? TryPeek(ServerRegistration registration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return cache.Peek(registration.Id);
        }
        catch (SnapshotCollectionException)
        {
            return null;
        }
    }

    private static ServerCard ToLiveCard(ServerRegistration registration, SnapshotCacheResult result)
    {
        var snapshot = result.Snapshot;
        var state = result.Freshness == SnapshotFreshness.Stale
            ? HealthState.Warning
            : snapshot.DatabaseTotal == 0
                ? HealthState.Unknown
                : snapshot.DatabaseOnline == snapshot.DatabaseTotal
                    ? HealthState.Healthy
                    : snapshot.DatabaseOnline == 0
                        ? HealthState.Critical
                        : HealthState.Warning;

        return new ServerCard(
            registration.Id.ToString("D"),
            snapshot.ServerName,
            snapshot.ProductVersion,
            snapshot.Edition,
            state,
            CpuPercent: null,
            snapshot.Memory?.SqlProcessMemoryUtilizationPercent,
            snapshot.DatabaseOnline,
            snapshot.DatabaseTotal,
            JobsHealthy: null,
            snapshot.Jobs?.TotalJobs,
            (int)Math.Clamp(result.Age.TotalSeconds, 0, int.MaxValue),
            result.Freshness == SnapshotFreshness.Fresh ? ServerDataSource.LiveFresh : ServerDataSource.LiveStale,
            snapshot.Jobs?.EnabledJobs,
            snapshot.Jobs?.FailedLastRun,
            snapshot.InstanceName,
            snapshot.UptimeSeconds,
            snapshot.CollectedAtUtc);
    }

    private static string TempDbDetail(TempDbDiagnostics diagnostics)
    {
        var used = diagnostics.UsedPercent.HasValue ? $"used {diagnostics.UsedPercent.Value:0.##}%" : "used unavailable";
        var recommendation = diagnostics.RecommendedFileCount.HasValue ? $"bounded file guidance {diagnostics.RecommendedFileCount.Value}" : "file guidance unavailable";
        var truncation = diagnostics.IsTruncated ? " · evidence truncated" : string.Empty;
        return $"{used} · size imbalance {diagnostics.SizeImbalancePercent:0.##}% · {recommendation}{truncation}; growth/contention not evaluated";
    }

    private static string HaDetail(HaDiagnostics diagnostics)
    {
        var lag = diagnostics.MaxSecondaryLagSeconds.HasValue ? $"max lag {diagnostics.MaxSecondaryLagSeconds.Value}s" : "lag unavailable";
        return $"Disconnected {diagnostics.DisconnectedReplicas} · unhealthy {diagnostics.UnhealthyReplicas} · unsynchronized DBs {diagnostics.UnsynchronizedDatabases} · suspended {diagnostics.SuspendedDatabases} · {lag}; quorum/RPO/RTO not evaluated";
    }

    private static ServerCard ToUnavailableCard(ServerRegistration registration) => new(
        registration.Id.ToString("D"),
        registration.DisplayName,
        "Not collected",
        "Registered target",
        HealthState.Unknown,
        CpuPercent: null,
        MemoryPercent: null,
        DatabaseOnline: 0,
        DatabaseTotal: 0,
        JobsHealthy: null,
        JobsTotal: null,
        LastScanSecondsAgo: 0,
        ServerDataSource.RegisteredUnavailable);
}
