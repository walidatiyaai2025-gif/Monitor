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
    DatabaseHealthDetailSnapshot? Databases = null,
    BackupHealthSnapshot? Backups = null,
    SqlAgentHealthSnapshot? Jobs = null,
    StorageHealthSnapshot? Storage = null,
    BlockingHealthSnapshot? Blocking = null,
    PerformanceHealthSnapshot? Performance = null);

public sealed record MemoryHealthSnapshot(
    long TotalPhysicalMemoryKb,
    long AvailablePhysicalMemoryKb,
    long SqlProcessPhysicalMemoryKb,
    int SqlProcessMemoryUtilizationPercent,
    bool IsPhysicalMemoryLow,
    bool IsVirtualMemoryLow,
    string SystemMemoryState,
    long? MaxServerMemoryMb = null,
    long? TotalServerMemoryKb = null,
    long? TargetServerMemoryKb = null,
    long? PageLifeExpectancySeconds = null,
    long? MemoryGrantsPending = null,
    string? TopMemoryClerkType = null,
    long? TopMemoryClerkKb = null);

public sealed record DatabaseHealthDetailSnapshot(int Restoring, int Recovering, int RecoveryPending, int Suspect, int Emergency, int OfflineOrOther);
public sealed record BackupHealthSnapshot(int BackedUpLast24Hours, int MissingFullBackupLast24Hours, DateTimeOffset? LastFullBackupAtUtc);
public sealed record SqlAgentHealthSnapshot(int TotalJobs, int EnabledJobs, int FailedLastRun);
public sealed record StorageHealthSnapshot(long TotalAllocatedBytes, long DataAllocatedBytes, long LogAllocatedBytes);
public sealed record BlockingHealthSnapshot(int BlockedRequests, long MaxWaitMilliseconds);
public sealed record PerformanceHealthSnapshot(int ActiveRequests, int RunnableTasks, int PendingIoRequests);

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
