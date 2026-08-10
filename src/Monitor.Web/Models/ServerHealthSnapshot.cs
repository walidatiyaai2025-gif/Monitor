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
    MemoryHealthSnapshot? Memory = null);

public sealed record MemoryHealthSnapshot(
    long TotalPhysicalMemoryKb,
    long AvailablePhysicalMemoryKb,
    long SqlProcessPhysicalMemoryKb,
    int SqlProcessMemoryUtilizationPercent,
    bool IsPhysicalMemoryLow,
    bool IsVirtualMemoryLow,
    string SystemMemoryState);

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
