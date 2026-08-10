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
        return new DashboardViewModel
        {
            Servers = servers,
            Incidents = incidentRows,
            Metrics =
            [
                new("Registered servers", servers.Count.ToString(), "Real SQL registrations", HealthState.Unknown),
                new("Databases online", $"{online} / {total}", "From cached SQL snapshots", online == total && total > 0 ? HealthState.Healthy : HealthState.Warning),
                new("Unavailable", servers.Count(item => item.Source == ServerDataSource.RegisteredUnavailable).ToString(), "Registered without a usable snapshot", HealthState.Warning)
            ],
            Activity = [new("Now", "Real estate projection loaded from the shared snapshot cache.", HealthState.Healthy)]
        };
    }

    public async Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var registrationId)) return demo.GetServer(id);

        var registration = registrations.GetById(registrationId);
        if (registration is null || !registration.IsEnabled) return null;

        var card = await TryGetLiveCardAsync(registration, cancellationToken) ?? ToUnavailableCard(registration);
        var live = card.Source is ServerDataSource.LiveFresh or ServerDataSource.LiveStale;
        return new ServerDetailsViewModel
        {
            Server = card,
            Metrics = live
                ?
                [
                    new("CPU", "Not collected", "Outside the current bounded snapshot", HealthState.Unknown),
                    new("Memory", $"{card.MemoryPercent}%", "SQL process memory utilization", card.MemoryPercent >= 85 ? HealthState.Warning : HealthState.Healthy),
                    new("Databases", $"{card.DatabaseOnline} / {card.DatabaseTotal}", "Online databases", card.State),
                    new("SQL Agent", "Not collected", "Outside the current bounded snapshot", HealthState.Unknown)
                ]
                :
                [
                    new("Snapshot", "Unavailable", "No usable cached snapshot is available yet", HealthState.Warning),
                    new("Connection", "Registered", "Retest safely from Connection Lab", HealthState.Unknown),
                    new("Credentials", "Protected", "Current secret reference is never rendered", HealthState.Unknown),
                    new("Collection", "Pending", "Refresh after connectivity and permissions are validated", HealthState.Unknown)
                ]
        };
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
                    snapshot.Jobs, snapshot.Storage, snapshot.Blocking, snapshot.Performance));
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
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var result = cache.Peek(registration.Id);
            if (result is null) return Task.FromResult<ServerCard?>(null);
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

            return Task.FromResult<ServerCard?>(new ServerCard(
                registration.Id.ToString("D"), snapshot.ServerName, snapshot.ProductVersion, snapshot.Edition, state,
                0, snapshot.Memory?.SqlProcessMemoryUtilizationPercent ?? 0,
                snapshot.DatabaseOnline, snapshot.DatabaseTotal, 0, 0,
                (int)Math.Clamp(result.Age.TotalSeconds, 0, int.MaxValue),
                result.Freshness == SnapshotFreshness.Fresh ? ServerDataSource.LiveFresh : ServerDataSource.LiveStale));
        }
        catch (SnapshotCollectionException)
        {
            return Task.FromResult<ServerCard?>(null);
        }
    }

    private static ServerCard ToUnavailableCard(ServerRegistration registration) => new(
        registration.Id.ToString("D"),
        registration.DisplayName,
        "Not collected",
        "Registered target",
        HealthState.Unknown,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        ServerDataSource.RegisteredUnavailable);
}
