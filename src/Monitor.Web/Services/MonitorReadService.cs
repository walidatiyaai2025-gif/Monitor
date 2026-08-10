using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IMonitorReadService
{
    Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default);
    Task<IncidentRecommendationViewModel?> GetRecommendationAsync(string incidentId, CancellationToken cancellationToken = default);
}

public sealed class MonitorReadService(
    IDemoMonitorService demo,
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IHealthRuleEvaluator? evaluator = null,
    IHealthIncidentRepository? incidents = null,
    IHealthRecommendationService? recommendations = null) : IMonitorReadService
{
    public async Task<IReadOnlyList<ServerCard>> GetServersAsync(
        CancellationToken cancellationToken = default)
    {
        var cards = demo.GetServers().ToArray();
        var registration = registrations.GetAll()
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        if (registration is null)
        {
            return cards;
        }

        var live = await TryGetLiveCardAsync(registration, cancellationToken);
        if (live is not null && cards.Length > 0)
        {
            cards[0] = live;
        }

        return cards;
    }

    public async Task<ServerDetailsViewModel?> GetServerAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var registrationId))
        {
            return demo.GetServer(id);
        }

        var registration = registrations.GetById(registrationId);
        if (registration is null || !registration.IsEnabled)
        {
            return null;
        }

        var card = await TryGetLiveCardAsync(registration, cancellationToken);
        if (card is null)
        {
            return null;
        }

        return new ServerDetailsViewModel
        {
            Server = card,
            Metrics =
            [
                new("CPU", "Not collected", "Outside the M1 identity snapshot", HealthState.Unknown),
                new("Memory", $"{card.MemoryPercent}%", "SQL process memory utilization", card.MemoryPercent >= 85 ? HealthState.Warning : HealthState.Healthy),
                new("Databases", $"{card.DatabaseOnline} / {card.DatabaseTotal}", "Online databases", card.State),
                new("SQL Agent", "Not collected", "Outside the M1 identity snapshot", HealthState.Unknown)
            ]
        };
    }

    public async Task<IReadOnlyList<HealthModuleServerViewModel>> GetHealthModulesAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<HealthModuleServerViewModel>();
        foreach (var registration in registrations.GetAll().Where(item => item.IsEnabled).OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id))
        {
            try
            {
                var result = await cache.GetAsync(registration, cancellationToken);
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

        return rows;
    }

    public async Task<IReadOnlyList<IncidentRow>> GetIncidentsAsync(CancellationToken cancellationToken = default)
    {
        await ReconcileIncidentsAsync(cancellationToken);

        return IncidentRepository().GetAll().Select(item => new IncidentRow(
            item.Id,
            item.Severity.ToString(),
            item.RegistrationId.ToString("D"),
            item.Title,
            $"{Math.Max(0, (DateTimeOffset.UtcNow - item.LastSeenUtc).TotalMinutes):0}m",
            item.Status.ToString())).ToArray();
    }

    public async Task<IncidentRecommendationViewModel?> GetRecommendationAsync(
        string incidentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            return null;
        }

        await ReconcileIncidentsAsync(cancellationToken);
        var incident = IncidentRepository().GetAll()
            .FirstOrDefault(item => string.Equals(item.Id, incidentId, StringComparison.Ordinal));
        if (incident is null)
        {
            return null;
        }

        var recommendation = (recommendations ??= new HealthRecommendationService()).Create(incident);
        return recommendation is null ? null : new IncidentRecommendationViewModel(incident, recommendation);
    }

    private async Task ReconcileIncidentsAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in registrations.GetAll().Where(item => item.IsEnabled))
        {
            try
            {
                var result = await cache.GetAsync(registration, cancellationToken);
                var findings = (evaluator ??= new HealthRuleEvaluator()).Evaluate(registration.Id, result.Snapshot, result.Freshness);
                IncidentRepository().Reconcile(
                    registration.Id, result.Snapshot.CollectedAtUtc, findings, result.Freshness == SnapshotFreshness.Fresh);
            }
            catch (SnapshotCollectionException)
            {
                // Collection failure does not invent or resolve incidents.
            }
        }
    }

    private IHealthIncidentRepository IncidentRepository() =>
        incidents ??= new InMemoryHealthIncidentRepository();

    private async Task<ServerCard?> TryGetLiveCardAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetAsync(registration, cancellationToken);
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
                0,
                snapshot.Memory?.SqlProcessMemoryUtilizationPercent ?? 0,
                snapshot.DatabaseOnline,
                snapshot.DatabaseTotal,
                0,
                0,
                (int)Math.Clamp(result.Age.TotalSeconds, 0, int.MaxValue),
                result.Freshness == SnapshotFreshness.Fresh
                    ? ServerDataSource.LiveFresh
                    : ServerDataSource.LiveStale);
        }
        catch (SnapshotCollectionException)
        {
            return null;
        }
    }
}
