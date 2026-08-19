namespace Monitor.Web.Services;

public sealed class InMemoryWebsiteProbeHistoryStore(TimeProvider timeProvider) : IWebsiteProbeHistoryStore
{
    private static readonly TimeSpan MaxRetention = TimeSpan.FromDays(30);
    private readonly object _gate = new();
    private readonly List<WebsiteProbeHistoryPoint> _points = [];

    public void Append(WebsiteProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var point = new WebsiteProbeHistoryPoint(
            result.TargetId,
            result.CompletedAtUtc,
            result.Classification.State,
            Bound(result.Classification.RuleId, 80),
            Bound(result.Classification.ProbableLayer, 120),
            Bound(result.Classification.Confidence, 16),
            result.Evidence.HttpStatusCode,
            result.Evidence.ElapsedMilliseconds,
            result.CertificateNotAfterUtc,
            Bound(result.FinalUri.DnsSafeHost, 253),
            result.RedirectCount,
            Bound(result.Classification.EvidenceSummary, 500));

        lock (_gate)
        {
            var cutoff = timeProvider.GetUtcNow() - MaxRetention;
            _points.RemoveAll(item => item.CompletedAtUtc < cutoff);
            if (!_points.Any(item => item.TargetId == point.TargetId && item.CompletedAtUtc == point.CompletedAtUtc))
                _points.Add(point);
            foreach (var group in _points.GroupBy(item => item.TargetId).ToArray())
            {
                var overflow = group.Count() - FileWebsiteProbeHistoryStore.MaxPerTarget;
                if (overflow <= 0) continue;
                foreach (var stale in group.OrderBy(item => item.CompletedAtUtc).Take(overflow).ToArray())
                    _points.Remove(stale);
            }
        }
    }

    public IReadOnlyList<WebsiteProbeHistoryPoint> Read(Guid targetId, TimeSpan window)
    {
        if (targetId == Guid.Empty || window <= TimeSpan.Zero) return [];
        var bounded = window > MaxRetention ? MaxRetention : window;
        lock (_gate)
        {
            var cutoff = timeProvider.GetUtcNow() - bounded;
            return _points.Where(item => item.TargetId == targetId && item.CompletedAtUtc >= cutoff)
                .OrderBy(item => item.CompletedAtUtc).ToArray();
        }
    }

    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
}

public sealed class InMemoryWebsiteScheduleStateStore : IWebsiteScheduleStateStore
{
    private sealed record State(DateTimeOffset NextDueUtc, string? LeaseToken, DateTimeOffset? LeaseUntilUtc);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, State> _states = [];

    public WebsiteProbeClaim? TryClaim(Guid targetId, DateTimeOffset nowUtc, TimeSpan interval, TimeSpan leaseDuration)
    {
        if (targetId == Guid.Empty) throw new ArgumentException("Target id is required.", nameof(targetId));
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        lock (_gate)
        {
            if (!_states.TryGetValue(targetId, out var state)) state = new(nowUtc, null, null);
            if (state.LeaseUntilUtc is DateTimeOffset leaseUntil && leaseUntil > nowUtc) return null;
            if (state.NextDueUtc > nowUtc) return null;
            var token = Guid.NewGuid().ToString("N");
            var claim = new WebsiteProbeClaim(targetId, token, nowUtc + leaseDuration);
            _states[targetId] = state with { LeaseToken = token, LeaseUntilUtc = claim.LeaseUntilUtc };
            return claim;
        }
    }

    public bool Complete(WebsiteProbeClaim claim, DateTimeOffset completedAtUtc, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        lock (_gate)
        {
            if (!_states.TryGetValue(claim.TargetId, out var state) || !string.Equals(state.LeaseToken, claim.Token, StringComparison.Ordinal))
                return false;
            _states[claim.TargetId] = new(completedAtUtc + interval, null, null);
            return true;
        }
    }
}

public sealed class WebsiteMonitoringWorker(
    WebsiteMonitoringOptions options,
    IWebsiteTargetStore targets,
    IWebsiteScheduleStateStore schedule,
    IWebsiteProbeEngine probe,
    IWebsiteProbeHistoryStore history,
    IWebsiteIncidentCoordinator incidentCoordinator,
    TimeProvider timeProvider,
    ILogger<WebsiteMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Website Monitoring scheduler is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SchedulerTickSeconds), timeProvider);
        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var enabledTargets = targets.GetAll().Where(item => item.IsEnabled).Take(FileWebsiteTargetStore.MaxTargets).ToArray();
        if (enabledTargets.Length == 0) return;

        using var concurrency = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
        var tasks = enabledTargets.Select(async target =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try { await RunTargetAsync(target, cancellationToken); }
            finally { concurrency.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task RunTargetAsync(WebsiteTargetDefinition target, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var interval = TimeSpan.FromSeconds(target.IntervalSeconds);
        var leaseDuration = TimeSpan.FromSeconds(Math.Min(300, target.TimeoutSeconds + 15));
        var claim = schedule.TryClaim(target.Id, now, interval, leaseDuration);
        if (claim is null) return;

        var completedAt = now;
        try
        {
            var result = await probe.ProbeAsync(target, cancellationToken);
            completedAt = result.CompletedAtUtc;
            history.Append(result);
            incidentCoordinator.Observe(target, result);
            logger.LogDebug("Website probe {TargetId} completed with {RuleId}/{State} in {ElapsedMs} ms.",
                target.Id, result.Classification.RuleId, result.Classification.State, result.Evidence.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            completedAt = timeProvider.GetUtcNow();
            logger.LogWarning(exception, "Website probe {TargetId} failed before a bounded result could be recorded.", target.Id);
        }
        finally
        {
            if (!schedule.Complete(claim, completedAt, interval))
                logger.LogWarning("Website probe claim {TargetId}/{ClaimToken} could not be completed because ownership changed.", claim.TargetId, claim.Token);
        }
    }
}
