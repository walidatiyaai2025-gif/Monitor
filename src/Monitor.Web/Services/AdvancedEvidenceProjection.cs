using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record TempDbDiagnostics(
    int FileCount,
    int TotalDataFiles,
    bool IsTruncated,
    double? UsedPercent,
    double SizeImbalancePercent,
    double? UsedImbalancePercent,
    double? ReadLatencyMs,
    double? WriteLatencyMs,
    int? RecommendedFileCount);

public sealed record TransactionLogDatabaseDiagnostics(
    string DatabaseKey,
    string RecoveryModel,
    bool HasDetailedStats,
    double? UsedPercent,
    LogVlfBand? VlfBand,
    string? ReuseWait,
    bool? TruncationBlocked,
    long? LogBackupAgeSeconds);

public sealed record HaDiagnostics(
    bool IsHadrEnabled,
    int TotalReplicas,
    int TotalDatabaseReplicas,
    bool ReplicasTruncated,
    bool DatabaseReplicasTruncated,
    int DisconnectedReplicas,
    int UnhealthyReplicas,
    int UnsynchronizedDatabases,
    int SuspendedDatabases,
    long? MaxSecondaryLagSeconds,
    long? MaxLogSendQueueKb,
    long? MaxRedoQueueKb);

public static class AdvancedEvidenceProjection
{
    public static TempDbDiagnostics? BuildTempDb(TempDbHealthSnapshot? evidence)
    {
        if (evidence is null) return null;
        var files = evidence.DataFiles?.Where(file => file.SizeBytes > 0).ToArray() ?? [];
        var recommendedFileCount = evidence.IsTruncated
            ? (int?)null
            : Batch400TempDbPressure.RecommendedFileCount(evidence.LogicalCpuCount, evidence.TotalDataFiles);
        if (files.Length == 0) return new(
            0,
            evidence.TotalDataFiles,
            evidence.IsTruncated,
            null,
            0,
            null,
            null,
            null,
            recommendedFileCount);

        var sizeMb = files.Select(file => file.SizeBytes / 1_048_576d).ToArray();
        var averageSize = sizeMb.Average();
        var sizeImbalance = sizeMb.Length < 2 || averageSize <= 0
            ? 0
            : Math.Round(Math.Clamp((sizeMb.Max() - sizeMb.Min()) * 100d / averageSize, 0, 1000), 2);

        double? usedPercent = null;
        double? usedImbalance = null;
        if (files.All(file => file.UsedBytes.HasValue))
        {
            var totalSize = files.Sum(file => (double)file.SizeBytes);
            var totalUsed = files.Sum(file => (double)file.UsedBytes!.Value);
            usedPercent = totalSize <= 0 ? null : Math.Round(Math.Clamp(totalUsed * 100d / totalSize, 0, 100), 2);
            var perFileUsed = files.Select(file => Math.Clamp(file.UsedBytes!.Value * 100d / file.SizeBytes, 0, 100)).ToArray();
            usedImbalance = perFileUsed.Length < 2 ? 0 : Math.Round(perFileUsed.Max() - perFileUsed.Min(), 2);
        }

        var reads = files.Sum(file => Math.Max(0, file.Reads));
        var writes = files.Sum(file => Math.Max(0, file.Writes));
        var readStall = files.Sum(file => Math.Max(0, file.ReadStallMs));
        var writeStall = files.Sum(file => Math.Max(0, file.WriteStallMs));

        return new(
            files.Length,
            evidence.TotalDataFiles,
            evidence.IsTruncated,
            usedPercent,
            sizeImbalance,
            usedImbalance,
            reads > 0 ? Math.Round(readStall / (double)reads, 2) : null,
            writes > 0 ? Math.Round(writeStall / (double)writes, 2) : null,
            recommendedFileCount);
    }

    public static IReadOnlyList<TransactionLogDatabaseDiagnostics> BuildTransactionLogs(TransactionLogHealthSnapshot? evidence)
    {
        if (evidence?.Databases is null) return [];
        return evidence.Databases.Select(database =>
        {
            double? usedPercent = null;
            LogVlfBand? vlfBand = null;
            string? reuseWait = null;
            bool? truncationBlocked = null;

            if (database.HasDetailedStats &&
                database.TotalLogSizeBytes is > 0 &&
                database.ActiveLogSizeBytes is >= 0 &&
                database.TotalVlfCount is > 0 &&
                !string.IsNullOrWhiteSpace(database.ReuseWait))
            {
                usedPercent = Batch400TransactionLogHealth.UsedPercent(
                    database.ActiveLogSizeBytes.Value / 1_048_576d,
                    database.TotalLogSizeBytes.Value / 1_048_576d);
                vlfBand = Batch400TransactionLogHealth.VlfBand((int)Math.Min(int.MaxValue, database.TotalVlfCount.Value));
                reuseWait = Batch400TransactionLogHealth.NormalizeReuseWait(database.ReuseWait);
                truncationBlocked = Batch400TransactionLogHealth.TruncationBlocked(database.ReuseWait);
            }

            return new TransactionLogDatabaseDiagnostics(
                database.DatabaseKey,
                database.RecoveryModel,
                database.HasDetailedStats,
                usedPercent,
                vlfBand,
                reuseWait,
                truncationBlocked,
                database.LogBackupAgeSeconds);
        }).ToArray();
    }

    public static HaDiagnostics? BuildHa(HaHealthSnapshot? evidence)
    {
        if (evidence is null) return null;
        var replicas = evidence.Replicas ?? [];
        var databases = evidence.DatabaseReplicas ?? [];

        return new(
            evidence.IsHadrEnabled,
            evidence.TotalReplicas,
            evidence.TotalDatabaseReplicas,
            evidence.ReplicasTruncated,
            evidence.DatabaseReplicasTruncated,
            replicas.Count(replica => string.Equals(replica.ConnectedState, "DISCONNECTED", StringComparison.OrdinalIgnoreCase)),
            replicas.Count(replica => string.Equals(replica.SynchronizationHealth, "NOT_HEALTHY", StringComparison.OrdinalIgnoreCase)),
            databases.Count(database => database.SynchronizationState is not null && !string.Equals(database.SynchronizationState, "SYNCHRONIZED", StringComparison.OrdinalIgnoreCase)),
            databases.Count(database => database.IsSuspended == true),
            MaxNullable(databases.Select(database => database.SecondaryLagSeconds)),
            MaxNullable(databases.Select(database => database.LogSendQueueKb)),
            MaxNullable(databases.Select(database => database.RedoQueueKb)));
    }

    private static long? MaxNullable(IEnumerable<long?> values)
    {
        var materialized = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return materialized.Length == 0 ? null : materialized.Max();
    }
}
