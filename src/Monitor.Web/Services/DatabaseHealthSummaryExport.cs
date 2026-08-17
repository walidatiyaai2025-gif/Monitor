using System.Globalization;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class DatabaseHealthSummaryExport
{
    private static readonly IReadOnlyList<string> Headers = ["Section", "Metric", "Value"];

    public static byte[] Build(ServerDetailsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var evidence = model.Evidence;
        var detail = evidence?.Databases;
        var projection = DatabaseStateProjection.Build(detail);
        var rows = new List<IReadOnlyList<string?>>();

        Add(rows, "Evidence", "State", model.Server.Source switch
        {
            ServerDataSource.LiveFresh => "Fresh",
            ServerDataSource.LiveStale => "Stale",
            _ => "Unavailable"
        });
        Add(rows, "Evidence", "CollectedAtUtc", evidence?.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Server", "DisplayLabel", model.Server.Name);

        Add(rows, "Aggregate", "Online", evidence is null ? "Unavailable" : model.Server.DatabaseOnline.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Aggregate", "Total", evidence is null ? "Unavailable" : model.Server.DatabaseTotal.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Aggregate", "Restoring", detail?.Restoring.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Aggregate", "Recovering", detail?.Recovering.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Aggregate", "RecoveryPending", detail?.RecoveryPending.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Aggregate", "Suspect", detail?.Suspect.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Aggregate", "Emergency", detail?.Emergency.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Aggregate", "OfflineOrOther", detail?.OfflineOrOther.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");

        Add(rows, "RetainedState", "State", projection.HasEvidence ? "Available" : "Unavailable");
        Add(rows, "RetainedState", "Rows", projection.HasEvidence ? projection.Items.Count.ToString(CultureInfo.InvariantCulture) : "Unavailable");
        Add(rows, "RetainedState", "WorstObserved", projection.HasEvidence ? projection.WorstObserved.ToString() : "Unavailable");
        Add(rows, "RetainedState", "Actionable", projection.HasEvidence ? projection.ActionableCount.ToString(CultureInfo.InvariantCulture) : "Unavailable");
        Add(rows, "RetainedState", "Unknown", projection.HasEvidence ? projection.UnknownCount.ToString(CultureInfo.InvariantCulture) : "Unavailable");

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static void Add(List<IReadOnlyList<string?>> rows, string section, string metric, string value) =>
        rows.Add([section, metric, value]);
}
