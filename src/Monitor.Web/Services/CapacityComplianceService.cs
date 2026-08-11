using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum ComplianceState
{
    Compliant,
    Warning,
    Critical,
    Unknown
}

public sealed record CapacityPolicy(
    long StorageCapacityBytes,
    int WarningStoragePercent = 80,
    int CriticalStoragePercent = 90,
    int MaxBackupAgeHours = 24,
    int MinimumDatabaseOnlinePercent = 100,
    int MinimumMemoryHeadroomPercent = 15)
{
    public void Validate()
    {
        if (StorageCapacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(StorageCapacityBytes));
        if (WarningStoragePercent is < 1 or > 99 || CriticalStoragePercent is < 2 or > 100 || WarningStoragePercent >= CriticalStoragePercent) throw new ArgumentOutOfRangeException(nameof(WarningStoragePercent));
        if (MaxBackupAgeHours is < 1 or > 168) throw new ArgumentOutOfRangeException(nameof(MaxBackupAgeHours));
        if (MinimumDatabaseOnlinePercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(MinimumDatabaseOnlinePercent));
        if (MinimumMemoryHeadroomPercent is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(MinimumMemoryHeadroomPercent));
    }
}

public sealed record CapacityComplianceProjection(
    Guid RegistrationId,
    int StorageUtilizationPercent,
    ComplianceState StorageState,
    ComplianceState BackupState,
    ComplianceState DatabaseState,
    ComplianceState MemoryState,
    int Score);

public sealed record EnvironmentComplianceRollup(
    ServerEnvironmentClass Environment,
    int Servers,
    int Compliant,
    int Warning,
    int Critical,
    int AverageScore);

public static class CapacityCompliance
{
    public static int StorageUtilizationPercent(ServerHealthSnapshot snapshot, CapacityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        var allocated = Math.Max(0, snapshot.Storage?.TotalAllocatedBytes ?? 0);
        return (int)Math.Clamp(Math.Round(100d * allocated / policy.StorageCapacityBytes, MidpointRounding.AwayFromZero), 0, 100);
    }

    public static ComplianceState StorageState(ServerHealthSnapshot snapshot, CapacityPolicy policy)
    {
        var percent = StorageUtilizationPercent(snapshot, policy);
        if (percent >= policy.CriticalStoragePercent) return ComplianceState.Critical;
        return percent >= policy.WarningStoragePercent ? ComplianceState.Warning : ComplianceState.Compliant;
    }

    public static ComplianceState BackupState(ServerHealthSnapshot snapshot, CapacityPolicy policy, DateTimeOffset now)
    {
        policy.Validate();
        var backups = snapshot.Backups;
        if (backups is null) return ComplianceState.Unknown;
        if (backups.MissingFullBackupLast24Hours > 0) return ComplianceState.Critical;
        if (backups.LastFullBackupAtUtc is null) return backups.BackedUpLast24Hours > 0 ? ComplianceState.Warning : ComplianceState.Critical;
        var age = now - backups.LastFullBackupAtUtc.Value;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age.TotalHours > policy.MaxBackupAgeHours * 2d) return ComplianceState.Critical;
        return age.TotalHours > policy.MaxBackupAgeHours ? ComplianceState.Warning : ComplianceState.Compliant;
    }

    public static ComplianceState DatabaseState(ServerHealthSnapshot snapshot, CapacityPolicy policy)
    {
        policy.Validate();
        if (snapshot.DatabaseTotal <= 0) return ComplianceState.Unknown;
        var percent = 100d * Math.Clamp(snapshot.DatabaseOnline, 0, snapshot.DatabaseTotal) / snapshot.DatabaseTotal;
        if (percent >= policy.MinimumDatabaseOnlinePercent) return ComplianceState.Compliant;
        return percent >= Math.Max(0, policy.MinimumDatabaseOnlinePercent - 10) ? ComplianceState.Warning : ComplianceState.Critical;
    }

    public static ComplianceState MemoryState(ServerHealthSnapshot snapshot, CapacityPolicy policy)
    {
        policy.Validate();
        var memory = snapshot.Memory;
        if (memory is null || memory.TotalPhysicalMemoryKb <= 0) return ComplianceState.Unknown;
        var headroom = 100d * Math.Max(0, memory.AvailablePhysicalMemoryKb) / memory.TotalPhysicalMemoryKb;
        if (memory.IsPhysicalMemoryLow || memory.IsVirtualMemoryLow || headroom < policy.MinimumMemoryHeadroomPercent / 2d) return ComplianceState.Critical;
        return headroom < policy.MinimumMemoryHeadroomPercent ? ComplianceState.Warning : ComplianceState.Compliant;
    }

    public static CapacityComplianceProjection Evaluate(ServerHealthSnapshot snapshot, CapacityPolicy policy, DateTimeOffset now)
    {
        var storage = StorageState(snapshot, policy);
        var backup = BackupState(snapshot, policy, now);
        var database = DatabaseState(snapshot, policy);
        var memory = MemoryState(snapshot, policy);
        var score = new[] { storage, backup, database, memory }.Sum(StatePoints);
        return new(snapshot.RegistrationId, StorageUtilizationPercent(snapshot, policy), storage, backup, database, memory, Math.Clamp(score, 0, 100));
    }

    public static IReadOnlyList<EnvironmentComplianceRollup> Rollup(
        IEnumerable<(ServerEnvironmentClass Environment, CapacityComplianceProjection Projection)> values) =>
        values.GroupBy(item => item.Environment)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var rows = group.Select(item => item.Projection).ToArray();
                return new EnvironmentComplianceRollup(
                    group.Key,
                    rows.Length,
                    rows.Count(IsFullyCompliant),
                    rows.Count(item => !IsFullyCompliant(item) && !HasCritical(item)),
                    rows.Count(HasCritical),
                    rows.Length == 0 ? 0 : (int)Math.Round(rows.Average(item => item.Score), MidpointRounding.AwayFromZero));
            }).ToArray();

    private static int StatePoints(ComplianceState state) => state switch
    {
        ComplianceState.Compliant => 25,
        ComplianceState.Warning => 15,
        ComplianceState.Unknown => 10,
        _ => 0
    };

    private static bool IsFullyCompliant(CapacityComplianceProjection item) =>
        item.StorageState == ComplianceState.Compliant && item.BackupState == ComplianceState.Compliant && item.DatabaseState == ComplianceState.Compliant && item.MemoryState == ComplianceState.Compliant;

    private static bool HasCritical(CapacityComplianceProjection item) =>
        item.StorageState == ComplianceState.Critical || item.BackupState == ComplianceState.Critical || item.DatabaseState == ComplianceState.Critical || item.MemoryState == ComplianceState.Critical;
}
