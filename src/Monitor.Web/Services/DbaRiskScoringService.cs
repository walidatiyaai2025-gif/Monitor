using Monitor.Web.Models;

namespace Monitor.Web.Services;

public enum DbaRiskLevel
{
    Low,
    Moderate,
    High,
    Critical
}

public sealed record DbaRiskComponents(
    int Freshness,
    int DatabaseAvailability,
    int BackupCompliance,
    int MemoryPressure,
    int Blocking,
    int RunnablePressure,
    int Incidents)
{
    public int Total => Math.Clamp(Freshness + DatabaseAvailability + BackupCompliance + MemoryPressure + Blocking + RunnablePressure + Incidents, 0, 100);
}

public sealed record DbaServerRisk(
    Guid RegistrationId,
    int Score,
    DbaRiskLevel Level,
    DbaRiskComponents Components,
    bool MaintenanceActive,
    bool AlertSuppressed,
    bool Actionable,
    DateTimeOffset EvaluatedAtUtc);

public static class DbaRiskScoring
{
    public static DbaServerRisk Evaluate(
        Guid registrationId,
        SnapshotCacheResult? cached,
        IEnumerable<HealthIncident> incidents,
        ServerOperatorMetadata metadata,
        DateTimeOffset now)
    {
        if (registrationId == Guid.Empty) throw new ArgumentException("Registration ID is required.", nameof(registrationId));
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.RegistrationId != registrationId) throw new ArgumentException("Operator metadata belongs to another registration.", nameof(metadata));

        var components = new DbaRiskComponents(
            FreshnessRisk(cached),
            DatabaseAvailabilityRisk(cached?.Snapshot),
            BackupComplianceRisk(cached?.Snapshot),
            MemoryPressureRisk(cached?.Snapshot),
            BlockingRisk(cached?.Snapshot),
            RunnablePressureRisk(cached?.Snapshot),
            IncidentRisk(registrationId, incidents));
        var score = components.Total;
        var maintenance = EnterpriseOperatorPolicy.IsMaintenanceActive(metadata, now);
        var suppressed = EnterpriseOperatorPolicy.IsAlertSuppressed(metadata, now);
        return new(
            registrationId,
            score,
            Classify(score),
            components,
            maintenance,
            suppressed,
            !maintenance && !suppressed,
            now);
    }

    public static DbaRiskLevel Classify(int score) => Math.Clamp(score, 0, 100) switch
    {
        >= 70 => DbaRiskLevel.Critical,
        >= 45 => DbaRiskLevel.High,
        >= 20 => DbaRiskLevel.Moderate,
        _ => DbaRiskLevel.Low
    };

    public static int FreshnessRisk(SnapshotCacheResult? cached) => cached switch
    {
        null => 25,
        { Freshness: SnapshotFreshness.Stale } => 15,
        _ => 0
    };

    public static int DatabaseAvailabilityRisk(ServerHealthSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.DatabaseTotal <= 0) return 0;
        var offline = Math.Clamp(snapshot.DatabaseTotal - snapshot.DatabaseOnline, 0, snapshot.DatabaseTotal);
        return (int)Math.Round(20d * offline / snapshot.DatabaseTotal, MidpointRounding.AwayFromZero);
    }

    public static int BackupComplianceRisk(ServerHealthSnapshot? snapshot)
    {
        var backups = snapshot?.Backups;
        if (backups is null) return 0;
        var total = Math.Max(0, backups.BackedUpLast24Hours) + Math.Max(0, backups.MissingFullBackupLast24Hours);
        if (total == 0) return 0;
        return (int)Math.Round(15d * Math.Max(0, backups.MissingFullBackupLast24Hours) / total, MidpointRounding.AwayFromZero);
    }

    public static int MemoryPressureRisk(ServerHealthSnapshot? snapshot)
    {
        var memory = snapshot?.Memory;
        if (memory is null) return 0;
        if (memory.IsPhysicalMemoryLow || memory.IsVirtualMemoryLow || memory.SqlProcessMemoryUtilizationPercent >= 90) return 15;
        if (memory.SqlProcessMemoryUtilizationPercent >= 80) return 10;
        return memory.SqlProcessMemoryUtilizationPercent >= 70 ? 5 : 0;
    }

    public static int BlockingRisk(ServerHealthSnapshot? snapshot)
    {
        var blocking = snapshot?.Blocking;
        if (blocking is null || blocking.BlockedRequests <= 0) return 0;
        if (blocking.BlockedRequests >= 10 || blocking.MaxWaitMilliseconds >= 60_000) return 10;
        return blocking.BlockedRequests >= 3 || blocking.MaxWaitMilliseconds >= 10_000 ? 7 : 4;
    }

    public static int RunnablePressureRisk(ServerHealthSnapshot? snapshot)
    {
        var runnable = snapshot?.Performance?.RunnableTasks ?? 0;
        if (runnable >= 8) return 10;
        if (runnable >= 4) return 7;
        return runnable >= 2 ? 4 : 0;
    }

    public static int IncidentRisk(Guid registrationId, IEnumerable<HealthIncident> incidents)
    {
        var relevant = incidents.Where(item => item.RegistrationId == registrationId && item.Status != IncidentStatus.Resolved).ToArray();
        if (relevant.Length == 0) return 0;
        var critical = relevant.Count(item => item.Severity == FindingSeverity.Critical);
        var warning = relevant.Length - critical;
        return Math.Clamp(critical * 8 + warning * 3, 0, 15);
    }
}

public sealed class DbaFleetRiskService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache,
    IHealthIncidentRepository incidents,
    IOperatorMetadataStore operatorMetadata,
    TimeProvider timeProvider)
{
    public IReadOnlyList<DbaServerRisk> Read()
    {
        var now = timeProvider.GetUtcNow();
        var incidentSnapshot = incidents.GetAll();
        return registrations.GetAll()
            .Select(registration => DbaRiskScoring.Evaluate(
                registration.Id,
                cache.Peek(registration.Id),
                incidentSnapshot,
                operatorMetadata.GetServer(registration.Id),
                now))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RegistrationId)
            .ToArray();
    }
}
