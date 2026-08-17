using System.Globalization;

namespace Monitor.Web.Services;

public sealed record SqlAgentHealthSummaryExportRow(
    string ServerLabel,
    string SnapshotState,
    DateTimeOffset? CollectedAtUtc,
    int? TotalJobs,
    int? EnabledJobs,
    int? FailedLastRun,
    string HistoryState,
    double? TopReliabilityScore,
    string? TopReliabilitySeverity,
    double? TopSuccessRatePercent,
    int? TopFailureStreak,
    double? TopP95DurationSeconds,
    double? TopDurationRegressionPercent,
    bool? TopAlertWorthy,
    int? TopRunsEvaluated,
    string ActivityState,
    int? CurrentActivityRows,
    int? RunningJobs,
    string ScheduleLatenessState);

public static class SqlAgentHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Server",
        "SnapshotState",
        "CollectedAtUtc",
        "TotalJobs",
        "EnabledJobs",
        "FailedLastRun",
        "HistoryState",
        "TopReliabilityScore",
        "TopReliabilitySeverity",
        "TopSuccessRatePercent",
        "TopFailureStreak",
        "TopP95DurationSeconds",
        "TopDurationRegressionPercent",
        "TopAlertWorthy",
        "TopRunsEvaluated",
        "ActivityState",
        "CurrentActivityRows",
        "RunningJobs",
        "ScheduleLatenessState"
    ];

    public static byte[] Build(IEnumerable<SqlAgentHealthSummaryExportRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rows = source.Select(item => (IReadOnlyList<string?>)
        [
            item.ServerLabel,
            item.SnapshotState,
            item.CollectedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TotalJobs?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.EnabledJobs?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.FailedLastRun?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.HistoryState,
            item.TopReliabilityScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopReliabilitySeverity ?? "Unavailable",
            item.TopSuccessRatePercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopFailureStreak?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopP95DurationSeconds?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopDurationRegressionPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Unavailable",
            item.TopAlertWorthy is bool alertWorthy ? (alertWorthy ? "true" : "false") : "Unavailable",
            item.TopRunsEvaluated?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.ActivityState,
            item.CurrentActivityRows?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.RunningJobs?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable",
            item.ScheduleLatenessState
        ]);

        return EnterpriseReportContract.Csv(Headers, rows);
    }
}
