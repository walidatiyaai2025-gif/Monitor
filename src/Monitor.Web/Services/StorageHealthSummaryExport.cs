using System.Globalization;
using Monitor.Web.Models;

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

public sealed class StorageHealthReportingService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache cache)
{
    public byte[] Build()
    {
        var rows = registrations.GetAll()
            .Where(registration => registration.IsEnabled)
            .OrderBy(registration => registration.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(registration => registration.Id)
            .Select(registration =>
            {
                try
                {
                    var cached = cache.Peek(registration.Id);
                    var snapshot = cached?.Snapshot;
                    var storage = snapshot?.Storage;
                    var io = IoLatencyProjection.Build(storage, snapshot?.UptimeSeconds, 20);
                    var top = io.FirstOrDefault();
                    var ioAvailable = io.Count > 0;

                    return new StorageHealthSummaryExportRow(
                        registration.DisplayName,
                        cached?.Freshness.ToString() ?? "Unavailable",
                        snapshot?.CollectedAtUtc,
                        storage is null ? "Unavailable" : "Available",
                        storage?.TotalAllocatedBytes,
                        storage?.DataAllocatedBytes,
                        storage?.LogAllocatedBytes,
                        snapshot is { UptimeSeconds: > 0 } ? snapshot.UptimeSeconds : null,
                        ioAvailable ? "Available" : "Unavailable",
                        ioAvailable ? io.Count : null,
                        ioAvailable ? io.Count(item => item.Hotspot) : null,
                        top?.Score,
                        top?.Severity.ToString(),
                        top?.WeightedLatencyMs,
                        top?.ThroughputMbPerSecond,
                        top?.WriteSharePercent,
                        top?.LatencyBand.ToString());
                }
                catch (SnapshotCollectionException)
                {
                    return new StorageHealthSummaryExportRow(
                        registration.DisplayName,
                        "Unavailable",
                        null,
                        "Unavailable",
                        null,
                        null,
                        null,
                        null,
                        "Unavailable",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);
                }
            });

        return StorageHealthSummaryExport.Build(rows);
    }
}

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
