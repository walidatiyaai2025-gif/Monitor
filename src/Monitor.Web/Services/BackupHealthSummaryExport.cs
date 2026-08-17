using System.Globalization;

namespace Monitor.Web.Services;

public sealed record BackupHealthSummaryExportRow(
    string ServerLabel,
    string SnapshotState,
    DateTimeOffset? CollectedAtUtc,
    int? CoveredLast24Hours,
    int? MissingFullBackupLast24Hours,
    DateTimeOffset? LastFullBackupAtUtc);

public static class BackupHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Server",
        "SnapshotState",
        "CollectedAtUtc",
        "CoveredLast24Hours",
        "MissingFullBackupLast24Hours",
        "LastFullBackupAtUtc",
        "ComplianceState"
    ];

    public static byte[] Build(IEnumerable<BackupHealthSummaryExportRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rows = source.Select(item => (IReadOnlyList<string?>)
        [
            item.ServerLabel,
            item.SnapshotState,
            item.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.CoveredLast24Hours?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.MissingFullBackupLast24Hours?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.LastFullBackupAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.CoveredLast24Hours.HasValue && item.MissingFullBackupLast24Hours.HasValue
                ? "NotEvaluated"
                : "Unavailable"
        ]);

        return EnterpriseReportContract.Csv(Headers, rows);
    }
}
