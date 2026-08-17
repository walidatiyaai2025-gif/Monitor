using System.Globalization;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class ServerIntelligenceExport
{
    private static readonly IReadOnlyList<string> Headers = ["Section", "Metric", "Value"];

    public static byte[] Build(ServerDetailsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var intelligence = ServerIntelligenceProjection.Build(model);
        var evidence = intelligence.EvidenceState == ServerIntelligenceEvidenceState.Unavailable
            ? null
            : model.Evidence;
        var rows = new List<IReadOnlyList<string?>>();

        Add(rows, "Evidence", "State", intelligence.EvidenceState.ToString());
        Add(rows, "Evidence", "CollectedAtUtc", evidence?.CollectedAtUtc.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable");

        Add(rows, "Identity", "DisplayLabel", intelligence.DisplayLabel);
        Add(rows, "Identity", "SqlMajor", intelligence.MajorVersion?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Identity", "VersionFamily", intelligence.MajorVersion.HasValue ? intelligence.VersionFamily : "Unavailable");
        Add(rows, "Identity", "SupportBaseline", intelligence.SupportedMajor switch
        {
            true => "Supported",
            false => "Legacy",
            _ => "Unavailable"
        });
        Add(rows, "Identity", "EditionClass", intelligence.EditionClass.ToString());
        Add(rows, "Identity", "UptimeBand", evidence is null ? "Unavailable" : intelligence.UptimeBand.ToString());

        if (intelligence.RuntimePressure is null)
        {
            Add(rows, "RuntimePressure", "State", "Unavailable");
            Add(rows, "RuntimePressure", "Score", "Unavailable");
            Add(rows, "RuntimePressure", "Classification", "Unavailable");
            Add(rows, "RuntimePressure", "Signals", "Unavailable");
        }
        else
        {
            Add(rows, "RuntimePressure", "State", "Available");
            Add(rows, "RuntimePressure", "Score", intelligence.RuntimePressure.Score.ToString(CultureInfo.InvariantCulture));
            Add(rows, "RuntimePressure", "Classification", intelligence.RuntimePressure.Classification.ToString());
            Add(rows, "RuntimePressure", "Signals", intelligence.RuntimePressure.Signals.Length == 0
                ? "None"
                : string.Join('|', intelligence.RuntimePressure.Signals));
        }

        Add(rows, "Database", "Online", evidence is null ? "Unavailable" : model.Server.DatabaseOnline.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Database", "Total", evidence is null ? "Unavailable" : model.Server.DatabaseTotal.ToString(CultureInfo.InvariantCulture));
        Add(rows, "Memory", "SqlProcessUtilizationPercent", evidence?.Memory?.SqlProcessMemoryUtilizationPercent.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Blocking", "BlockedRequests", evidence?.Blocking?.BlockedRequests.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Blocking", "MaxWaitMilliseconds", evidence?.Blocking?.MaxWaitMilliseconds.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Performance", "RunnableTasks", evidence?.Performance?.RunnableTasks.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
        Add(rows, "Performance", "PendingIoRequests", evidence?.Performance?.PendingIoRequests.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static void Add(List<IReadOnlyList<string?>> rows, string section, string metric, string value) =>
        rows.Add([section, metric, value]);
}
