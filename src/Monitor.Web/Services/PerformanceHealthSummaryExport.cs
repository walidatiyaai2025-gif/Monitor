using System.Globalization;

namespace Monitor.Web.Services;

public sealed record PerformanceHealthSummaryExportRow(
    string ServerLabel,
    string SnapshotState,
    DateTimeOffset? CollectedAtUtc,
    int? ActiveRequests,
    int? RunnableTasks,
    int? PendingIoRequests,
    string WaitEvidenceState,
    string? TopWaitCategory,
    double? TopWaitScore,
    string? TopWaitSeverity,
    double? TopWaitMsPerSecond,
    double? TopWaitSharePercent,
    double? TopWaitSignalPercent);

public static class PerformanceHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Server",
        "SnapshotState",
        "CollectedAtUtc",
        "ActiveRequests",
        "RunnableTasks",
        "PendingIoRequests",
        "WaitEvidenceState",
        "TopWaitCategory",
        "TopWaitScore",
        "TopWaitSeverity",
        "TopWaitMsPerSecond",
        "TopWaitSharePercent",
        "TopWaitSignalPercent"
    ];

    public static byte[] Build(IEnumerable<PerformanceHealthSummaryExportRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rows = source.Select(item => (IReadOnlyList<string?>)
        [
            item.ServerLabel,
            item.SnapshotState,
            item.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.ActiveRequests?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.RunnableTasks?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.PendingIoRequests?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.WaitEvidenceState,
            item.TopWaitCategory ?? "Unavailable",
            item.TopWaitScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopWaitSeverity ?? "Unavailable",
            item.TopWaitMsPerSecond?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopWaitSharePercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopWaitSignalPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable"
        ]);

        return EnterpriseReportContract.Csv(Headers, rows);
    }
}
