using System.Globalization;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class DatabaseHealthExport
{
    public const int MaxServerRows = 250;

    private static readonly IReadOnlyList<string> Headers =
    [
        "RecordType",
        "Server",
        "Source",
        "AgeSeconds",
        "DatabaseOnline",
        "DatabaseTotal",
        "DetailState",
        "WorstObserved",
        "ActionableCount",
        "UnknownCount",
        "Restoring",
        "Recovering",
        "RecoveryPending",
        "Suspect",
        "Emergency",
        "OfflineOrOther",
        "Value"
    ];

    public static byte[] Build(IReadOnlyList<HealthModuleServerViewModel> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);

        var ordered = servers
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(server => server.Id, StringComparer.Ordinal)
            .ToArray();
        var selected = ordered.Take(MaxServerRows).ToArray();
        var rows = new List<IReadOnlyList<string?>>(selected.Length + 1)
        {
            Row(
                "Coverage",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                $"ObservedServers={ordered.Length};ExportedServers={selected.Length};Truncated={(ordered.Length > selected.Length ? "Yes" : "No")}")
        };

        foreach (var server in selected)
        {
            var aggregateAvailable = server.Source is ServerDataSource.LiveFresh or ServerDataSource.LiveStale or ServerDataSource.Demo;
            var detail = aggregateAvailable ? server.Databases : null;
            var aggregateDetailAvailable = detail is not null;
            var projection = DatabaseStateProjection.Build(detail);
            var retainedDetailAvailable = projection.HasEvidence;

            rows.Add(Row(
                "Server",
                server.Name,
                server.Source.ToString(),
                aggregateAvailable ? server.AgeSeconds.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateAvailable ? server.DatabaseOnline.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateAvailable ? server.DatabaseTotal.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                retainedDetailAvailable ? "Available" : "Unavailable",
                retainedDetailAvailable ? projection.WorstObserved.ToString() : "Unavailable",
                retainedDetailAvailable ? projection.ActionableCount.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                retainedDetailAvailable ? projection.UnknownCount.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.Restoring.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.Recovering.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.RecoveryPending.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.Suspect.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.Emergency.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                aggregateDetailAvailable ? detail!.OfflineOrOther.ToString(CultureInfo.InvariantCulture) : "Unavailable",
                null));
        }

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static IReadOnlyList<string?> Row(
        string recordType,
        string? server,
        string? source,
        string? ageSeconds,
        string? databaseOnline,
        string? databaseTotal,
        string? detailState,
        string? worstObserved,
        string? actionableCount,
        string? unknownCount,
        string? restoring,
        string? recovering,
        string? recoveryPending,
        string? suspect,
        string? emergency,
        string? offlineOrOther,
        string? value) =>
        [
            recordType,
            server,
            source,
            ageSeconds,
            databaseOnline,
            databaseTotal,
            detailState,
            worstObserved,
            actionableCount,
            unknownCount,
            restoring,
            recovering,
            recoveryPending,
            suspect,
            emergency,
            offlineOrOther,
            value
        ];
}
