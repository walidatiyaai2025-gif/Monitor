using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IMonitorReadService
{
    Task<IReadOnlyList<ServerCard>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<ServerDetailsViewModel?> GetServerAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class MonitorReadService(
    IDemoMonitorService demo,
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache) : IMonitorReadService
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
                new("CPU", "Not collected", "Outside the current snapshot", HealthState.Unknown),
                new("Memory", $"{card.MemoryPercent}%", "SQL process memory utilization", card.MemoryPercent >= 85 ? HealthState.Warning : HealthState.Healthy),
                new("Databases", $"{card.DatabaseOnline} / {card.DatabaseTotal}", DatabaseDetail(card), card.State),
                new("SQL Agent", "Not collected", "Outside the current snapshot", HealthState.Unknown)
            ]
        };
    }

    private static string DatabaseDetail(ServerCard card)
    {
        var detail = card.DatabaseHealth;
        if (detail is null)
        {
            return "Online databases";
        }

        return detail.UnavailableCount == 0
            ? $"All online · {detail.ReadOnlyCount} read-only"
            : $"{detail.UnavailableCount} unavailable · {detail.RecoveryCount} recovering · {detail.CriticalCount} critical";
    }

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
                    : ServerDataSource.LiveStale,
                snapshot.DatabaseHealth);
        }
        catch (SnapshotCollectionException)
        {
            return null;
        }
    }
}
