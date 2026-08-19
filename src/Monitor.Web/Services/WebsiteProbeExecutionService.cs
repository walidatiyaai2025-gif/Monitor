using System.Collections.Concurrent;

namespace Monitor.Web.Services;

public sealed record WebsiteProbeExecutionAttempt(
    bool Executed,
    WebsiteProbeResult? Result,
    WebsiteIncidentObservation Observation)
{
    public static WebsiteProbeExecutionAttempt Busy { get; } =
        new(false, null, WebsiteIncidentObservation.None);
}

public interface IWebsiteProbeExecutionService
{
    Task<WebsiteProbeExecutionAttempt> TryExecuteAsync(
        WebsiteTargetDefinition target,
        CancellationToken cancellationToken = default);
}

public sealed class WebsiteProbeExecutionService(
    DistributedCoordinationOptions coordination,
    IDistributedLeaseManager distributedLeases,
    IWebsiteProbeEngine probe,
    IWebsiteProbeHistoryStore history,
    IWebsiteIncidentCoordinator incidentCoordinator,
    IWebsiteNotificationPlanner notificationPlanner,
    ILogger<WebsiteProbeExecutionService> logger) : IWebsiteProbeExecutionService
{
    private static readonly TimeSpan DistributedProbeLease = TimeSpan.FromSeconds(120);
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _localGates = new();

    public async Task<WebsiteProbeExecutionAttempt> TryExecuteAsync(
        WebsiteTargetDefinition target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var validation = WebsiteTargetValidator.Validate(target);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(target));

        var local = _localGates.GetOrAdd(target.Id, _ => new SemaphoreSlim(1, 1));
        if (!await local.WaitAsync(0, cancellationToken))
            return WebsiteProbeExecutionAttempt.Busy;

        DistributedLeaseHandle? distributed = null;
        try
        {
            if (coordination.Enabled)
            {
                distributed = await distributedLeases.TryAcquireAsync(
                    $"website.probe.{target.Id:N}",
                    DistributedProbeLease,
                    cancellationToken);
                if (distributed is null)
                    return WebsiteProbeExecutionAttempt.Busy;
            }

            var result = await probe.ProbeAsync(target, cancellationToken);
            history.Append(result);
            var observation = incidentCoordinator.Observe(target, result);
            _ = notificationPlanner.Queue(target, result, observation);
            return new WebsiteProbeExecutionAttempt(true, result, observation);
        }
        finally
        {
            if (distributed is not null)
            {
                try
                {
                    if (!await distributedLeases.ReleaseAsync(distributed, CancellationToken.None))
                    {
                        logger.LogWarning(
                            "Website distributed probe lease for {TargetId} could not be released because persisted ownership changed.",
                            target.Id);
                    }
                }
                catch (Exception exception) when (exception is SharedStateStoreUnavailableException or InvalidDataException or InvalidOperationException)
                {
                    logger.LogWarning(
                        exception,
                        "Website distributed probe lease release failed for {TargetId}; expiry remains the safety boundary.",
                        target.Id);
                }
            }

            local.Release();
        }
    }
}
