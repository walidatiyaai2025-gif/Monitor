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
    PerformanceHealthSnapshot? Performance = null,
    TempDbHealthSnapshot? TempDb = null,
    TransactionLogHealthSnapshot? TransactionLogs = null);

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

public sealed record DatabaseStateSnapshot(string Name, string State);
public sealed record DatabaseHealthDetailSnapshot(
    int Restoring,
    int Recovering,
    int RecoveryPending,
    int Suspect,
    int Emergency,
    int OfflineOrOther,
    IReadOnlyList<DatabaseStateSnapshot>? Items = null);
public sealed record BackupHealthSnapshot(int BackedUpLast24Hours, int MissingFullBackupLast24Hours, DateTimeOffset? LastFullBackupAtUtc);
public sealed record AgentJobRunSnapshot(
    string JobKey,
    string Owner,
    bool Succeeded,
    long RunOrder,
    long DurationSeconds);
public sealed record AgentScheduleSnapshot(
    string JobKey,
    DateTime? NextScheduledRunLocal,
    bool IsRunning);
public sealed record SqlAgentHealthSnapshot(
    int TotalJobs,
    int EnabledJobs,
    int FailedLastRun,
    IReadOnlyList<AgentJobRunSnapshot>? RecentRuns = null,
    IReadOnlyList<AgentScheduleSnapshot>? Schedules = null);
public sealed record IoFileSnapshot(
    string FileKey,
    long Reads,
    long Writes,
    long ReadStallMs,
    long WriteStallMs,
    long BytesRead,
    long BytesWritten);
public sealed record StorageHealthSnapshot(
    long TotalAllocatedBytes,
    long DataAllocatedBytes,
    long LogAllocatedBytes,
    IReadOnlyList<IoFileSnapshot>? IoFiles = null);
public sealed record BlockingHealthSnapshot(int BlockedRequests, long MaxWaitMilliseconds);
public sealed record WaitStatSnapshot(string WaitType, long WaitTimeMs, long SignalWaitTimeMs, long WaitingTasks);
public sealed record PerformanceHealthSnapshot(
    int ActiveRequests,
    int RunnableTasks,
    int PendingIoRequests,
    IReadOnlyList<WaitStatSnapshot>? Waits = null);

public sealed record TempDbFileSnapshot(
    int FileId,
    string FileKey,
    long SizeBytes,
    long? UsedBytes,
    long Reads,
    long Writes,
    long ReadStallMs,
    long WriteStallMs);

public sealed record TempDbHealthSnapshot(
    int LogicalCpuCount,
    int TotalDataFiles,
    IReadOnlyList<TempDbFileSnapshot>? DataFiles = null,
    bool IsTruncated = false);

public sealed record TransactionLogDatabaseSnapshot(
    string DatabaseName,
    string RecoveryModel,
    string LogReuseWait,
    long TotalLogBytes,
    DateTimeOffset? LastFullBackupAtUtc,
    DateTimeOffset? LastLogBackupAtUtc);

public sealed record TransactionLogHealthSnapshot(
    int TotalUserDatabases,
    IReadOnlyList<TransactionLogDatabaseSnapshot>? Databases = null,
    bool IsTruncated = false);

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
