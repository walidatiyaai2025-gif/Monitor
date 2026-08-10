using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record DbaOperationsSurfaceViewModel(
    ApplicationReadinessStatus ReadinessStatus,
    string ReadinessMessage,
    DeploymentTopology DeploymentMode,
    string NodeLabel,
    SharedStateReadinessStatus SharedStateStatus,
    int? SharedStateSchemaVersion,
    bool SharedStorageReady,
    string BackupStatus,
    int BackupCount,
    string? LatestBackupId,
    DateTimeOffset? LatestBackupUtc,
    bool SchedulerEnabled,
    bool SchedulerRunning,
    string SchedulerRole,
    int SchedulerAttempted,
    int SchedulerSucceeded,
    int SchedulerFailed,
    int SchedulerSkipped,
    DateTimeOffset CheckedAtUtc)
{
    public string ReadinessCss => ReadinessStatus switch
    {
        ApplicationReadinessStatus.Ready => "healthy",
        ApplicationReadinessStatus.Degraded => "warning",
        _ => "critical"
    };

    public string SharedStateCss => SharedStateStatus switch
    {
        SharedStateReadinessStatus.Ready => "healthy",
        SharedStateReadinessStatus.Disabled => "unknown",
        SharedStateReadinessStatus.SchemaMismatch => "warning",
        _ => "critical"
    };

    public string BackupCss => string.Equals(BackupStatus, "Ready", StringComparison.OrdinalIgnoreCase)
        ? "healthy"
        : "warning";

    public string SchedulerCss => !SchedulerEnabled
        ? "unknown"
        : SchedulerFailed > 0
            ? "warning"
            : SchedulerRunning
                ? "healthy"
                : "unknown";
}

public interface IDbaOperationsSurfaceService
{
    Task<DbaOperationsSurfaceViewModel> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class DbaOperationsSurfaceService(
    IApplicationReadinessService readiness,
    IOperationalBackupService backups,
    ISchedulerStatusStore scheduler,
    SnapshotScheduleOptions scheduleOptions,
    DeploymentTopologyOptions deploymentOptions) : IDbaOperationsSurfaceService
{
    public async Task<DbaOperationsSurfaceViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var readinessSnapshot = await readiness.CheckAsync(cancellationToken);
        var backup = backups.GetReadiness();
        var schedulerStatus = scheduler.Get();
        var latest = backup.RecentBackups
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        return new(
            readinessSnapshot.Status,
            readinessSnapshot.Message,
            deploymentOptions.Mode,
            BuildOpaqueNodeLabel(),
            readinessSnapshot.SharedStateStatus,
            readinessSnapshot.SharedStateSchemaVersion,
            readinessSnapshot.SharedStorageReady,
            backup.Status,
            backup.BackupCount,
            latest?.BackupId,
            backup.LatestBackupUtc,
            scheduleOptions.Enabled,
            schedulerStatus.Running,
            ResolveSchedulerRole(scheduleOptions.Enabled, schedulerStatus.Running),
            schedulerStatus.Attempted,
            schedulerStatus.Succeeded,
            schedulerStatus.Failed,
            schedulerStatus.SkippedBackoff,
            readinessSnapshot.CheckedAtUtc);
    }

    private static string ResolveSchedulerRole(bool enabled, bool running) =>
        !enabled ? "Disabled" : running ? "Active cycle" : "Passive / idle";

    private static string BuildOpaqueNodeLabel()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName));
        return $"NODE-{Convert.ToHexString(bytes.AsSpan(0, 4))}";
    }
}
