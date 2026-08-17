using System.Globalization;

namespace Monitor.Web.Services;

public static class FleetDecisionSupportExport
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Section",
        "Metric",
        "Value",
        "ClusterKey",
        "DominantRule",
        "Severity",
        "Score",
        "AffectedServers",
        "Environments",
        "BucketUtc"
    ];

    public static byte[] Build(FleetIntelligenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var rows = new List<IReadOnlyList<string?>>();
        AddMetric(rows, "Evidence", "IncidentEvidenceComplete", Boolean(snapshot.IncidentEvidenceComplete));
        AddMetric(rows, "Evidence", "IncidentEvidenceLimit", Number(snapshot.IncidentEvidenceLimit));
        AddMetric(rows, "Evidence", "ServerPolicyEvidenceComplete", Boolean(snapshot.ServerPolicyEvidenceComplete));
        AddMetric(rows, "Evidence", "IncidentPolicyEvidenceComplete", Boolean(snapshot.IncidentPolicyEvidenceComplete));
        AddMetric(rows, "Evidence", "OperatorPolicyUnavailable", Number(snapshot.OperatorPolicyUnavailable));
        AddMetric(rows, "Evidence", "IncidentRiskAvailable", Boolean(snapshot.IncidentRisk is not null));
        AddMetric(rows, "Evidence", "DecisionSupportAvailable", Boolean(snapshot.DecisionSupport is not null));

        AddRisk(rows, snapshot.IncidentRisk);
        AddDecisionSupport(rows, snapshot.DecisionSupport);

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static void AddRisk(List<IReadOnlyList<string?>> rows, Batch300FleetRiskSummary? risk)
    {
        if (risk is null)
        {
            AddMetric(rows, "FleetRisk", "State", "Unavailable");
            return;
        }

        AddMetric(rows, "FleetRisk", "Score", Number(risk.Score));
        AddMetric(rows, "FleetRisk", "Level", risk.Level.ToString());
        AddMetric(rows, "FleetRisk", "ActionableCount", Number(risk.ActionableCount));
        AddMetric(rows, "FleetRisk", "SuppressedCount", Number(risk.SuppressedCount));
        AddMetric(rows, "FleetRisk", "TopRuleKeys", string.Join('|', risk.TopKeys));
    }

    private static void AddDecisionSupport(List<IReadOnlyList<string?>> rows, FleetDecisionSupportSnapshot? decisionSupport)
    {
        if (decisionSupport is null)
        {
            AddMetric(rows, "DecisionSupport", "State", "Unavailable");
            return;
        }

        AddMetric(rows, "DecisionSupport", "InputIncidents", Number(decisionSupport.InputIncidents));
        AddMetric(rows, "DecisionSupport", "CorrelationWindowMinutes", decisionSupport.CorrelationWindow.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture));

        if (decisionSupport.RoutingSummary is null)
        {
            AddMetric(rows, "Routing", "State", "Unavailable");
        }
        else
        {
            var routing = decisionSupport.RoutingSummary;
            AddMetric(rows, "Routing", "EvaluatedIncidents", Number(routing.EvaluatedIncidents));
            AddMetric(rows, "Routing", "Page", Number(routing.Page));
            AddMetric(rows, "Routing", "Notify", Number(routing.Notify));
            AddMetric(rows, "Routing", "Queue", Number(routing.Queue));
            AddMetric(rows, "Routing", "None", Number(routing.None));
            AddMetric(rows, "Routing", "Suppressed", Number(routing.Suppressed));
            AddMetric(rows, "Routing", "InMaintenance", Number(routing.InMaintenance));
            AddMetric(rows, "Routing", "Unassigned", Number(routing.Unassigned));
        }

        if (decisionSupport.CorrelationSummary is null)
        {
            AddMetric(rows, "Correlation", "State", "Unavailable");
        }
        else
        {
            var correlation = decisionSupport.CorrelationSummary;
            AddMetric(rows, "Correlation", "EvaluatedIncidents", Number(correlation.EvaluatedIncidents));
            AddMetric(rows, "Correlation", "TotalClusters", Number(correlation.TotalClusters));
            AddMetric(rows, "Correlation", "CriticalClusters", Number(correlation.CriticalClusters));
            AddMetric(rows, "Correlation", "WarningClusters", Number(correlation.WarningClusters));
            AddMetric(rows, "Correlation", "InfoClusters", Number(correlation.InfoClusters));
            AddMetric(rows, "Correlation", "MultiServerClusters", Number(correlation.MultiServerClusters));
            AddMetric(rows, "Correlation", "MaxAffectedServers", Number(correlation.MaxAffectedServers));
            AddMetric(rows, "Correlation", "HighestScore", correlation.HighestScore.ToString("0.##", CultureInfo.InvariantCulture));
        }

        foreach (var cluster in decisionSupport.Correlations.Take(FleetDecisionSupport.MaxItems))
        {
            rows.Add(
            [
                "CorrelationDetail",
                "Cluster",
                null,
                cluster.ClusterKey,
                cluster.DominantRule,
                cluster.Severity.ToString(),
                cluster.Score.ToString("0.##", CultureInfo.InvariantCulture),
                Number(cluster.AffectedServers),
                string.Join('|', cluster.Environments),
                cluster.BucketUtc.ToString("O", CultureInfo.InvariantCulture)
            ]);
        }
    }

    private static void AddMetric(List<IReadOnlyList<string?>> rows, string section, string metric, string? value) =>
        rows.Add([section, metric, value, null, null, null, null, null, null, null]);

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
