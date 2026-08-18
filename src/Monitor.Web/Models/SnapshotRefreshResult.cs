namespace Monitor.Web.Models;

public enum SnapshotFreshness
{
    Fresh,
    Stale
}

public enum SnapshotRefreshStatus
{
    Refreshed,
    RetainedStale,
    Throttled,
    RegistrationNotFound,
    Disabled
}

public sealed record SnapshotRefreshResult(
    SnapshotRefreshStatus Status,
    string Message,
    int RetryAfterSeconds = 0,
    SnapshotFreshness? Freshness = null);
