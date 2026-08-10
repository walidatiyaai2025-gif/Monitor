namespace Monitor.Web.Models;

public sealed record ServerHealthSnapshot(
    Guid RegistrationId,
    string ServerName,
    string ProductVersion,
    string Edition,
    string? InstanceName,
    long UptimeSeconds,
    int DatabaseTotal,
    int DatabaseOnline,
    DateTimeOffset CollectedAtUtc,
    MemoryHealthSnapshot? Memory = null,
    DatabaseHealthSnapshot? DatabaseHealth = null);

public sealed record MemoryHealthSnapshot(
    long TotalPhysicalMemoryKb,
    long AvailablePhysicalMemoryKb,
    long SqlProcessPhysicalMemoryKb,
    int SqlProcessMemoryUtilizationPercent,
    bool IsPhysicalMemoryLow,
    bool IsVirtualMemoryLow,
    string SystemMemoryState);

public sealed record DatabaseHealthSnapshot(
    int OnlineCount,
    int RestoringCount,
    int RecoveringCount,
    int RecoveryPendingCount,
    int SuspectCount,
    int EmergencyCount,
    int OfflineCount,
    int CopyingCount,
    int OfflineSecondaryCount,
    int OtherCount,
    int ReadOnlyCount)
{
    public int TotalCount =>
        OnlineCount + RestoringCount + RecoveringCount + RecoveryPendingCount +
        SuspectCount + EmergencyCount + OfflineCount + CopyingCount +
        OfflineSecondaryCount + OtherCount;

    public int UnavailableCount => TotalCount - OnlineCount;

    public int RecoveryCount => RestoringCount + RecoveringCount + RecoveryPendingCount;

    public int CriticalCount => SuspectCount + EmergencyCount + OfflineCount;
}

public enum SnapshotCollectionFailure
{
    Disabled,
    SecretUnavailable,
    TimedOut,
    AuthenticationFailed,
    NetworkUnavailable,
    CertificateRejected,
    Failed
}

public sealed class SnapshotCollectionException(
    SnapshotCollectionFailure failure,
    string message) : Exception(message)
{
    public SnapshotCollectionFailure Failure { get; } = failure;
}
