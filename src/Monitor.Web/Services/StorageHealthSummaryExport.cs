using System.Globalization;

namespace Monitor.Web.Services;

public sealed record StorageHealthSummaryExportRow(
    string ServerLabel,
    string SnapshotState,
    DateTimeOffset? CollectedAtUtc,
    string AllocationEvidenceState,
    long? TotalAllocatedBytes,
    long? DataAllocatedBytes,
    long? LogAllocatedBytes,
    long? UptimeSeconds,
    string IoEvidenceState,
    int? IoFilesEvaluated,
    int? HotspotCount,
    double? TopIoScore,
    string? TopIoSeverity,
    double? TopWeightedLatencyMs,
    double? TopThroughputMbPerSecond,
    double? TopWriteSharePercent,
    string? TopLatencyBand);

public static class StorageHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Server",
        "SnapshotState",
        "CollectedAtUtc",
        "AllocationEvidenceState",
        "TotalAllocatedBytes",
        "DataAllocatedBytes",
        "LogAllocatedBytes",
        "UptimeSeconds",
        "IoEvidenceState",
        "IoFilesEvaluated",
        "HotspotCount",
        "TopIoScore",
        "TopIoSeverity",
        "TopWeightedLatencyMs",
        "TopThroughputMbPerSecond",
        "TopWriteSharePercent",
        "TopLatencyBand"
    ];

    public static byte[] Build(IEnumerable<StorageHealthSummaryExportRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rows = source.Select(item => (IReadOnlyList<string?>)
        [
            item.ServerLabel,
            item.SnapshotState,
            item.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.AllocationEvidenceState,
            item.TotalAllocatedBytes?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.DataAllocatedBytes?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.LogAllocatedBytes?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.UptimeSeconds?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.IoEvidenceState,
            item.IoFilesEvaluated?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.HotspotCount?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopIoScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopIoSeverity ?? "Unavailable",
            item.TopWeightedLatencyMs?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopThroughputMbPerSecond?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopWriteSharePercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopLatencyBand ?? "Unavailable"
        ]);

        return EnterpriseReportContract.Csv(Headers, rows);
    }
}
